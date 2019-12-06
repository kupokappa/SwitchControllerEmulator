using System;
using System.Collections;
using System.IO.Ports;
using UnityEngine;
using UnityEngine.UI;
using CustomExtensions;

public class SerialComms : MonoBehaviour {
	public UIHandler uIHandler;
	public InputManager Inputs;
	public Text mouseInputDebug;
	private static SerialPort _port = new SerialPort();

	public bool beginConnect = false;
	public bool bridgeReset = false;

	public float waitTime = 1f / 120f;

	private bool inSync = false;
	private bool packetSent;
	private bool waitingForSync = true;
	private bool busy = false;

	private int inWaiting;

	private readonly byte[] defaultPacket = new byte[] { 0x00, 0x00, 0x08, 0x80, 0x80, 0x80, 0x80, 0x00 };
	private byte[] receivedPacket;

	private long previousCMD = NOINPUT;
	private long outputCMD;

	private float mouseX = 0;
	private float mouseY = 0;

	private byte byteRead;
	private byte latestByte;

	//Commands to send to the MCU
	private enum MCU_CMD : byte {
		NOP = 0x00,
		SYNC_1 = 0x33,
		SYNC_2 = 0xCC,
		SYNC_START = 0xFF
	}

	//Responses from the MCU
	private enum MCU_RESP : byte {
		USB_ACK = 0x90,
		UPDATE_ACK = 0x91,
		UPDATE_NACK = 0x92,
		SYNC_START = 0xFF,
		SYNC_1 = 0xCC,
		SYNC_OK = 0x33
	}

	//Enum DPAD DIR values
	private enum DPAD_DIR : int {
		CENTER = 0x00,
		U = 0x01,
		R = 0x02,
		D = 0x04,
		L = 0x08,
		U_R = (U + R),
		D_R = (D + R),
		U_L = (U + L),
		D_L = (D + L)
	}

	//Actual DPAD values
	private enum A_DPAD : byte {
		DPAD_CENTER = 0x08,
		DPAD_U = 0x00,
		DPAD_U_R = 0x01,
		DPAD_R = 0x02,
		DPAD_D_R = 0x03,
		DPAD_D = 0x04,
		DPAD_D_L = 0x05,
		DPAD_L = 0x06,
		DPAD_U_L = 0x07
	}

	//Buttons
	private enum BTN : int {
		NONE = 0x0000000000000000,
		Y = 0x0000000000000001,
		B = 0x0000000000000002,
		A = 0x0000000000000004,
		X = 0x0000000000000008,
		L = 0x0000000000000010,
		R = 0x0000000000000020,
		ZL = 0x0000000000000040,
		ZR = 0x0000000000000080,
		MINUS = 0x0000000000000100,
		PLUS = 0x0000000000000200,
		LCLICK = 0x0000000000000400,
		RCLICK = 0x0000000000000800,
		HOME = 0x0000000000001000,
		CAPTURE = 0x0000000000002000
	}

	//Raw DPAD values
	private enum DPAD_RAW : int {
		CENTER = 0x0000000000000000,
		U = 0x0000000000010000,
		R = 0x0000000000020000,
		D = 0x0000000000040000,
		L = 0x0000000000080000,
		U_R = U + R,
		D_R = D + R,
		D_L = D + L
	}

	private enum LSTICK : long {
		CENTER = 0x0000000000000000,
		R = 0x00000000FF000000,
		U_R = 0x0000002DFF000000,
		U = 0x0000005AFF000000,
		U_L = 0x00000087FF000000,
		L = 0x000000B4FF000000,
		D_L = 0x000000E1FF000000,
		D = 0x0000010EFF000000,
		D_R = 0x0000013BFF000000
	}

	private enum RSTICK : long {
		CENTER = 0x0000000000000000,
		R = 0x000FF00000000000,
		U_R = 0x02DFF00000000000,
		U = 0x05AFF00000000000,
		U_L = 0x087FF00000000000,
		L = 0x0B4FF00000000000,
		D_L = 0x0E1FF00000000000,
		D = 0x10EFF00000000000,
		D_R = 0x13BFF00000000000
	}

	private const int NOINPUT = (int)BTN.NONE + (int)DPAD_RAW.CENTER + (int)LSTICK.CENTER + (int)RSTICK.CENTER;

	private Tuple<long, long> Angle(long angle, long intensity) {
		long x = (long)((Math.Cos(MathExtensions.ToRadians(angle)) * 0x7F) * intensity / 0xFF) + 0x80;
		long y = -(long)((Math.Sin(MathExtensions.ToRadians(angle)) * 0x7F) * intensity / 0xFF) + 0x80;
		return Tuple.Create(x, y);
	}

	private long LStickAngle(long angle, long intensity) {
		return (intensity + (angle << 8)) << 24;
	}

	private long RStickAngle(long angle, long intensity) {
		return (intensity + (angle << 8)) << 44;
	}

	private IEnumerator WaitForData(int timeout = 1000, float sleepTime = 0.01f) {
		inWaiting = _port.BytesToRead;

		DebugMessage("Waiting for data...");
		print("Waiting for data...");
		while (inWaiting == 0) {
			yield return new WaitForSecondsRealtime(sleepTime);
			inWaiting = _port.BytesToRead;
		}
		DebugMessage("Data received (" + inWaiting + " bytes.)");
		print("Received " + inWaiting + " bytes");
		yield break;
	}

	//Read x bytes from serial port.
	private IEnumerator ReadBytes(int size) {
		receivedPacket = new byte[size];
		byte[] bytesIn = new byte[size];

		try {
			_port.Read(bytesIn, 0, size);
		}
		catch (System.IO.IOException ex) {
			UnityEngine.Debug.LogError("An error occurred: " + ex.Message);
			DebugMessage("An error occurred: " + ex.Message);
			print("o fucc");
			//StartCoroutine(Sync());
			ResetBridge();
			yield break;
		}
		bytesIn.CopyTo(receivedPacket, 0);

		string s = "0x";
		foreach (byte b in receivedPacket) {
			s += b.ToString("X");
		}
		print(s);
		yield break;
	}

	//Read a single byte from the buffer.
	private IEnumerator ReadByte() {
		byteRead = 0;

		yield return StartCoroutine(ReadBytes(1));

		if (receivedPacket.Length != 0) {
			byteRead = receivedPacket[0];
		}
		else if (receivedPacket.Length == 0) {
			byteRead = 0;
		}
	}

	private IEnumerator ReadByteLatest() {
		latestByte = 0;
		inWaiting = _port.BytesToRead;
		print(inWaiting + " bytes in waiting.");
		byte byteIn;

		if (inWaiting == 0) {
			inWaiting = 1;
			print("0 bytes in waiting.");
		}

		yield return StartCoroutine(ReadBytes(inWaiting));

		if (receivedPacket.Length != 0) {
			byteIn = receivedPacket[0];
		}
		else
			byteIn = 0;

		latestByte = byteIn;
	}

	private IEnumerator WriteBytes(byte[] bytesOut) {
		_port.BaseStream.Write(bytesOut, 0, bytesOut.Length);
		yield break;
	}

	private IEnumerator WriteByte(byte byteOut) {
		byte[] singleByte = new byte[1];
		singleByte[0] = byteOut;

		yield return StartCoroutine(WriteBytes(singleByte));
		print("wrote single byte " + singleByte[0].ToString("X"));

		yield break;
	}

	private static byte _crc8_ccitt(byte inCRC, byte inData) {
		byte i;
		int data;

		data = (inCRC ^ inData);

		for (i = 0; i < 8; i++) {
			if ((data & 0x80) != 0) {
				data <<= 1;
				data ^= 0x07;
			}
			else {
				data <<= 1;
			}
		}
		return (byte)data;
	}

	//Send a raw packet and wait for a response (CRC will be added automatically)
	private IEnumerator SendPacket(byte[] packet) {
		packetSent = false;
		byte[] bytesOut = new byte[9];

		print("packet length is " + packet.Length);
		print("bytesOut length is " + bytesOut.Length);

		byte byteIn;

		//Compute CRC
		byte crc = 0;
		foreach (byte d in packet) {
			crc = _crc8_ccitt(crc, d);
		}
		packet.CopyTo(bytesOut, 0);
		bytesOut[8] = crc;

		string packetOutput = "Wrote packet 0x";
		foreach (byte b in bytesOut) {
			packetOutput += b.ToString("X");
			
		}
		print(packetOutput);

		yield return WriteBytes(bytesOut);

		//Wait for USB ACK or UDPATE NACK
		yield return StartCoroutine(ReadByte());

		byteIn = byteRead;

		print("Response read: 0x" + byteIn.ToString("X"));
		bool commandSuccess = (byteIn == (byte)MCU_RESP.USB_ACK);
		packetSent = commandSuccess;

		if (!commandSuccess)
			print("Failed to send command.");
		if (commandSuccess)
			print("Command sent successfully.");
		yield break;
	}

	//Force sync if normal sync fails.
	private IEnumerator ForceSync() {
		DebugMessage("Beginning force sync...");

		//Send 9 0xFF bytes to fully flush out the buffer on device
		//Device will send back 0xFF (RESP_SYNC_START) when it's ready to sync
		byte[] flush = new byte[9] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

		DebugMessage("Flushing MCU buffer");
		print("Flushing MCU buffer.");
		_port.BaseStream.WriteAsync(flush, 0, flush.Length);

		//Wait for serial data and read the last byte sent
		DebugMessage("Starting WaitForData()...");
		yield return StartCoroutine(WaitForData());
		yield return StartCoroutine(ReadByteLatest());

		byte byteIn = latestByte;

		//Begin sync
		inSync = false;

		if (byteIn == (byte)MCU_RESP.SYNC_START) {
			DebugMessage("Received 0x" + byteIn.ToString("X") + " (MCU_RESP.SYNC_START)");
			yield return StartCoroutine(WriteByte((byte)MCU_CMD.SYNC_1));
			yield return StartCoroutine(ReadByte());
			byteIn = byteRead;

			if (byteIn == (byte)MCU_RESP.SYNC_1) {
				DebugMessage("Received 0x" + byteIn.ToString("X") + " (MCU_RESP.SYNC_1)");
				yield return StartCoroutine(WriteByte((byte)MCU_CMD.SYNC_2));
				yield return StartCoroutine(ReadByte());
				byteIn = byteRead;

				if (byteIn == (byte)MCU_RESP.SYNC_OK) {
					DebugMessage("Received 0x" + byteIn.ToString("X") + " (MCU_RESP.SYNC_OK)");
					DebugMessage("Sync OK");
					inSync = true;
				}
			}
		}
	}

	//Sync program to MCU
	private IEnumerator Sync() {
		yield return StartCoroutine(ConnectToPort());
		inSync = false;

		uIHandler.resetButton.gameObject.SetActive(true);
		uIHandler.resetButton.interactable = true;

		//Try sending a packet
		yield return StartCoroutine(SendPacket(defaultPacket));

		if (!inSync) {
			DebugMessage("Normal sync failed. Attempting to force sync.");
			yield return StartCoroutine(ForceSync());
			if (inSync) {
				DebugMessage("Sending default packet...");
				yield return StartCoroutine(SendPacket(defaultPacket));
				DebugMessage("Default packet sent. MCU responded with 0x" + byteRead.ToString("X"));
				if (byteRead == 0x90)
					DebugMessage("(MCU_RESP.USB_ACK)");
			}
		}
		yield break;
	}

	private void DebugMessage(string message) {
		string oldMessage = uIHandler.debugText.text;
		string newMessage = message + "\n" + oldMessage;
		uIHandler.debugText.text = newMessage;
	}

	//Initialize serial port connection before syncing.
	private IEnumerator ConnectToPort() {
		try {
			DebugMessage("Connecting to serial port...");
			UnityEngine.Debug.Log("Connecting to serial port...");
			_port.PortName = uIHandler.comPort;
			DebugMessage("Port opened on " + uIHandler.comPort);
			UnityEngine.Debug.Log("Port opened on " + uIHandler.comPort);
			_port.BaudRate = 19200;
			UnityEngine.Debug.Log("Set baud rate to 19200");
			DebugMessage("Set baud rate to 19200");
			_port.ReadTimeout = 1000;
			UnityEngine.Debug.Log("Set read timeout to " + _port.ReadTimeout / 1000 + " seconds.");
			DebugMessage("Set read timeout to " + _port.ReadTimeout / 1000 + " seconds.");
			_port.WriteTimeout = 1000;
			UnityEngine.Debug.Log("Set write timeout to " + _port.WriteTimeout / 1000 + " seconds.");
			DebugMessage("Set write timeout to " + _port.WriteTimeout / 1000 + " seconds.");
			_port.Handshake = Handshake.RequestToSend;
			_port.DtrEnable = true;
			_port.RtsEnable = true;
			_port.Parity = Parity.Even;
			_port.StopBits = StopBits.One;
			_port.Open();
			DebugMessage("Serial port ready.");
			UnityEngine.Debug.Log("Serial port ready.");

			beginConnect = false;

			uIHandler.syncButton.gameObject.SetActive(false);
			uIHandler.input.gameObject.SetActive(false);
		}
		catch (System.IO.IOException ex) {
			DebugMessage("An error occurred: " + ex.Message);
			UnityEngine.Debug.LogError("An error occurred: " + ex.Message);
			beginConnect = false;
		}
		yield break;
	}

	private IEnumerator WaitForReset() {
		yield return new WaitForSecondsRealtime(5);
		if (!inSync) {
			DebugMessage("Operation timed out.");
			bridgeReset = false;
			StopAllCoroutines();
		}
		bridgeReset = false;
	}

	private void ResetBridge() {
		try {
			StopAllCoroutines();
			inSync = false;
			_port.Close();
			StartCoroutine(Sync());
			StartCoroutine(WaitForReset());
			bridgeReset = false;
		} catch (Exception ex) {
			DebugMessage("An error occurred when resetting: " + ex.Message);
		}
	}

	private IEnumerator InputHandlerDebug() {
		if (Input.GetKeyDown(KeyCode.P)) {
			yield return StartCoroutine(TestBenchLStick());
		}
		if (Input.GetKeyDown(KeyCode.O)) {
			yield return StartCoroutine(TestBenchRStick());
		}
		if (Input.GetKeyDown(KeyCode.I)) {
			yield return StartCoroutine(TestBenchBTN());
		}

		if (Input.GetKeyDown(KeyCode.W)) {
			StartCoroutine(SendCMD((long)LSTICK.U));
		}
		if (Input.GetKeyUp(KeyCode.W)) {
			StartCoroutine(SendCMD());
		}
		if (Input.GetKeyDown(KeyCode.A)) {
			StartCoroutine(SendCMD((long)LSTICK.L));
		}
		if (Input.GetKeyUp(KeyCode.A)) {
			StartCoroutine(SendCMD());
		}
		if (Input.GetKeyDown(KeyCode.S)) {
			StartCoroutine(SendCMD((long)LSTICK.D));
		}
		if (Input.GetKeyUp(KeyCode.S)) {
			StartCoroutine(SendCMD());
		}
		if (Input.GetKeyDown(KeyCode.D)) {
			StartCoroutine(SendCMD((long)LSTICK.R));
		}
		if (Input.GetKeyUp(KeyCode.D)) {
			StartCoroutine(SendCMD());
		}

		if (Input.GetKeyDown(KeyCode.UpArrow))
			StartCoroutine(SendCMD((long)DPAD_RAW.U));
		if (Input.GetKeyUp(KeyCode.UpArrow))
			StartCoroutine(SendCMD());
		if (Input.GetKeyDown(KeyCode.RightArrow))
			StartCoroutine(SendCMD((long)DPAD_RAW.R));
		if (Input.GetKeyUp(KeyCode.RightArrow))
			StartCoroutine(SendCMD());
		if (Input.GetKeyDown(KeyCode.LeftArrow))
			StartCoroutine(SendCMD((long)DPAD_RAW.L));
		if (Input.GetKeyUp(KeyCode.LeftArrow))
			StartCoroutine(SendCMD());
		if (Input.GetKeyDown(KeyCode.DownArrow))
			StartCoroutine(SendCMD((long)DPAD_RAW.D));
		if (Input.GetKeyUp(KeyCode.DownArrow))
			StartCoroutine(SendCMD());
	}

	private long DecryptDPAD(long dpad) {
		byte dpadDecrypt = 0;

		if (dpad == (int)DPAD_DIR.U)
			dpadDecrypt = (int)A_DPAD.DPAD_U;
		else if (dpad == (int)DPAD_DIR.R)
			dpadDecrypt = (int)A_DPAD.DPAD_R;
		else if (dpad == (int)DPAD_DIR.D)
			dpadDecrypt = (int)A_DPAD.DPAD_D;
		else if (dpad == (int)DPAD_DIR.L)
			dpadDecrypt = (int)A_DPAD.DPAD_L;
		else if (dpad == (int)DPAD_DIR.U_L)
			dpadDecrypt = (int)A_DPAD.DPAD_U_L;
		else if (dpad == (int)DPAD_DIR.U_R)
			dpadDecrypt = (int)A_DPAD.DPAD_U_R;
		else if (dpad == (int)DPAD_DIR.D_R)
			dpadDecrypt = (int)A_DPAD.DPAD_D_R;
		else if (dpad == (int)DPAD_DIR.D_L)
			dpadDecrypt = (int)A_DPAD.DPAD_D_L;
		else
			dpadDecrypt = (byte)A_DPAD.DPAD_CENTER;
		return dpadDecrypt;
	}

	private byte[] CMDToPacket(long command) {
		long cmdCopy = command;

		long low = (cmdCopy & 0XFF); cmdCopy = cmdCopy >> 8;
		long high = (cmdCopy & 0xFF); cmdCopy = cmdCopy >> 8;
		long dpad = (cmdCopy & 0XFF); cmdCopy = cmdCopy >> 8;
		long lstickIntensity = (cmdCopy & 0xFF); cmdCopy = cmdCopy >> 8;
		long lstickAngle = (cmdCopy & 0xFFF); cmdCopy = cmdCopy >> 12;
		long rstickIntensity = (cmdCopy & 0xFF); cmdCopy = cmdCopy >> 8;
		long rstickAngle = (cmdCopy & 0xFFF);
		dpad = DecryptDPAD(dpad);
		long leftX = Angle(lstickAngle, lstickIntensity).Item1;
		long leftY = Angle(lstickAngle, lstickIntensity).Item2;
		long rightX = Angle(rstickAngle, rstickIntensity).Item1;
		long rightY = Angle(rstickAngle, rstickIntensity).Item2;


		byte[] packet = new byte[] { (byte)high, (byte)low, (byte)dpad, (byte)leftX, (byte)leftY, (byte)rightX, (byte)rightY, 0x00 };

		string s = "Command to be sent: 0x";
		foreach (byte b in packet) {
			s += b.ToString("X");
		}
		print(s);

		return packet;
	}

	private IEnumerator SendCMD(long command = NOINPUT) {
		yield return StartCoroutine(SendPacket(CMDToPacket(command)));
	}

	private IEnumerator Wait(float time) {
		yield return new WaitForSecondsRealtime(time);
	}

	//Test Left Analog Stick
	private IEnumerator TestBenchLStick() {
		DebugMessage("Testing left stick...");

		//Test U/R/L/D
		StartCoroutine(SendCMD((long)BTN.LCLICK)); yield return StartCoroutine(Wait(0.5f)); StartCoroutine(SendCMD()); yield return StartCoroutine(Wait(0.001f));
		StartCoroutine(SendCMD((long)LSTICK.U)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)LSTICK.R)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)LSTICK.L)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)LSTICK.D)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)LSTICK.CENTER)); yield return StartCoroutine(Wait(0.5f));

		//360 circle, full intensity
		for (int i = 0; i < 721; i++) {
			print(i.ToString());
			long cmd = LStickAngle(i + 90, 0xFF);
			StartCoroutine(SendCMD(cmd));
			yield return StartCoroutine(Wait(0.001f));
		}
		StartCoroutine(SendCMD((long)LSTICK.CENTER));

		//360 circle, half intensity
		for (int i = 0; i < 721; i++) {
			long cmd = LStickAngle(i + 90, 0x80);
			StartCoroutine(SendCMD(cmd));
			yield return StartCoroutine(Wait(0.001f));
		}
		StartCoroutine(SendCMD((long)LSTICK.CENTER)); StartCoroutine(Wait(0.5f));

		DebugMessage("Finished testing left stick.");

		yield break;
	}

	//Test Right Analog Stick
	private IEnumerator TestBenchRStick() {
		DebugMessage("Testing right stick...");
		//Test U/R/L/D
		StartCoroutine(SendCMD((long)BTN.RCLICK)); yield return StartCoroutine(Wait(0.5f)); StartCoroutine(SendCMD()); yield return StartCoroutine(Wait(0.001f));
		StartCoroutine(SendCMD((long)RSTICK.U)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)RSTICK.R)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)RSTICK.L)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)RSTICK.D)); yield return StartCoroutine(Wait(0.5f));
		StartCoroutine(SendCMD((long)RSTICK.CENTER)); yield return StartCoroutine(Wait(0.5f));

		//360 circle, full intensity
		for (int i = 0; i < 721; i++) {
			print(i.ToString());
			long cmd = RStickAngle(i + 90, 0xFF);
			StartCoroutine(SendCMD(cmd));
			yield return StartCoroutine(Wait(0.001f));
		}
		StartCoroutine(SendCMD((long)RSTICK.CENTER));

		//360 circle, half intensity
		for (int i = 0; i < 721; i++) {
			long cmd = RStickAngle(i + 90, 0x80);
			StartCoroutine(SendCMD(cmd));
			yield return StartCoroutine(Wait(0.001f));
		}
		StartCoroutine(SendCMD((long)RSTICK.CENTER)); StartCoroutine(Wait(0.5f));

		DebugMessage("Finished testing right stick.");
		yield break;
	}

	//Test all buttons
	private IEnumerator TestBenchBTN() {
		DebugMessage("Testing buttons...");

		//Test all buttons
		StartCoroutine(SendCMD((long)BTN.A)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.B)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.X)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.Y)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));

		StartCoroutine(SendCMD((long)BTN.L)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.R)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.ZL)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.ZR)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));

		StartCoroutine(SendCMD((long)BTN.PLUS)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.MINUS)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.LCLICK)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.RCLICK)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));

		StartCoroutine(SendCMD((long)DPAD_RAW.U)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)DPAD_RAW.D)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)DPAD_RAW.L)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)DPAD_RAW.R)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));

		StartCoroutine(SendCMD((long)BTN.HOME)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));
		StartCoroutine(SendCMD((long)BTN.CAPTURE)); yield return StartCoroutine(Wait(0.2f)); StartCoroutine(SendCMD()); StartCoroutine(Wait(0.2f));

		DebugMessage("Finished testing buttons.");

		yield break;
	}

	private void Start() {
		//InvokeRepeating("UpdateControllerState", 1f, waitTime);
	}

	private void GetInputs() {
		//Face buttons
		if (Input.GetKeyDown(Inputs.SW_A)) {
			outputCMD += (long)BTN.A;
		}
		if (Input.GetKeyUp(Inputs.SW_A)) {
			outputCMD -= (long)BTN.A;
		}
		if (Input.GetKeyDown(Inputs.SW_B)) {
			outputCMD += (long)BTN.B;
		}
		if (Input.GetKeyUp(Inputs.SW_B)) {
			outputCMD -= (long)BTN.B;
		}
		if (Input.GetKeyDown(Inputs.SW_X)) {
			outputCMD += (long)BTN.X;
		}
		if (Input.GetKeyUp(Inputs.SW_X)) {
			outputCMD -= (long)BTN.X;
		}
		if (Input.GetKeyDown(Inputs.SW_Y)) {
			outputCMD += (long)BTN.Y;
		}
		if (Input.GetKeyUp(Inputs.SW_Y)) {
			outputCMD -= (long)BTN.Y;
		}

		//Shoulder buttons
		if (Input.GetKeyDown(Inputs.SW_L)) {
			outputCMD += (long)BTN.L;
		}
		if (Input.GetKeyUp(Inputs.SW_L)) {
			outputCMD -= (long)BTN.L;
		}
		if (Input.GetKeyDown(Inputs.SW_R)) {
			outputCMD += (long)BTN.R;
		}
		if (Input.GetKeyUp(Inputs.SW_R)) {
			outputCMD -= (long)BTN.R;
		}
		if (Input.GetKeyDown(Inputs.SW_ZL)) {
			outputCMD += (long)BTN.ZL;
		}
		if (Input.GetKeyUp(Inputs.SW_ZL)) {
			outputCMD -= (long)BTN.ZL;
		}
		if (Input.GetKeyDown(Inputs.SW_ZR)) {
			outputCMD += (long)BTN.ZR;
		}
		if (Input.GetKeyUp(Inputs.SW_ZR)) {
			outputCMD -= (long)BTN.ZR;
		}

		//More face buttons
		if (Input.GetKeyDown(Inputs.SW_PL)) {
			outputCMD += (long)BTN.PLUS;
		}
		if (Input.GetKeyUp(Inputs.SW_PL)) {
			outputCMD -= (long)BTN.PLUS;
		}
		if (Input.GetKeyDown(Inputs.SW_MN)) {
			outputCMD += (long)BTN.MINUS;
		}
		if (Input.GetKeyUp(Inputs.SW_MN)) {
			outputCMD -= (long)BTN.MINUS;
		}
		if (Input.GetKeyDown(Inputs.SW_CP)) {
			outputCMD += (long)BTN.CAPTURE;
		}
		if (Input.GetKeyUp(Inputs.SW_CP)) {
			outputCMD -= (long)BTN.CAPTURE;
		}
		if (Input.GetKeyDown(Inputs.SW_HM)) {
			outputCMD += (long)BTN.HOME;
		}
		if (Input.GetKeyUp(Inputs.SW_HM)) {
			outputCMD -= (long)BTN.HOME;
		}

		//Stick buttons
		if (Input.GetKeyDown(Inputs.SW_LC)) {
			outputCMD += (long)BTN.LCLICK;
		}
		if (Input.GetKeyUp(Inputs.SW_LC)) {
			outputCMD -= (long)BTN.LCLICK;
		}
		if (Input.GetKeyDown(Inputs.SW_RC)) {
			outputCMD += (long)BTN.RCLICK;
		}
		if (Input.GetKeyUp(Inputs.SW_RC)) {
			outputCMD -= (long)BTN.RCLICK;
		}

		//DPAD
		if (Input.GetKeyDown(Inputs.SW_D_U)) {
			outputCMD += (long)DPAD_RAW.U;
		}
		if (Input.GetKeyUp(Inputs.SW_D_U)) {
			outputCMD -= (long)DPAD_RAW.U;
		}
		if (Input.GetKeyDown(Inputs.SW_D_D)) {
			outputCMD += (long)DPAD_RAW.D;
		}
		if (Input.GetKeyUp(Inputs.SW_D_D)) {
			outputCMD -= (long)DPAD_RAW.D;
		}
		if (Input.GetKeyDown(Inputs.SW_D_L)) {
			outputCMD += (long)DPAD_RAW.L;
		}
		if (Input.GetKeyUp(Inputs.SW_D_L)) {
			outputCMD -= (long)DPAD_RAW.L;
		}
		if (Input.GetKeyDown(Inputs.SW_D_R)) {
			outputCMD += (long)DPAD_RAW.R;
		}
		if (Input.GetKeyUp(Inputs.SW_D_R)) {
			outputCMD -= (long)DPAD_RAW.R;
		}

		//Left stick
		if (Input.GetKeyDown(Inputs.SW_LS_U)) {
			outputCMD += (long)LSTICK.U;
		}
		if (Input.GetKeyUp(Inputs.SW_LS_U)) {
			outputCMD -= (long)LSTICK.U;
		}
		if (Input.GetKeyDown(Inputs.SW_LS_D)) {
			outputCMD += (long)LSTICK.D;
		}
		if (Input.GetKeyUp(Inputs.SW_LS_D)) {
			outputCMD -= (long)LSTICK.D;
		}
		if (Input.GetKeyDown(Inputs.SW_LS_L)) {
			outputCMD += (long)LSTICK.L;
		}
		if (Input.GetKeyUp(Inputs.SW_LS_L)) {
			outputCMD -= (long)LSTICK.L;
		}
		if (Input.GetKeyDown(Inputs.SW_LS_R)) {
			outputCMD += (long)LSTICK.R;
		}
		if (Input.GetKeyUp(Inputs.SW_LS_R)) {
			outputCMD -= (long)LSTICK.R;
		}

		mouseX += Input.GetAxis("Mouse X");
		mouseY += Input.GetAxis("Mouse Y");

		mouseInputDebug.text = $"X = {mouseX}, Y = {mouseY}";
	}

	private IEnumerator UpdateControllerState() {
		busy = true;
		if (outputCMD != previousCMD) {
			yield return StartCoroutine(SendCMD(outputCMD));
			previousCMD = outputCMD;
		}
		yield return new WaitForSecondsRealtime(waitTime);
		busy = false;
	}

	private void Update() {
		if (beginConnect) {
			StartCoroutine(Sync());
		}
		if (inSync) {
			waitingForSync = false;
			GetInputs();
			if (!busy) {
				StartCoroutine(UpdateControllerState());
			}
		}
		if (bridgeReset) {
			ResetBridge();
		}
	}
}
