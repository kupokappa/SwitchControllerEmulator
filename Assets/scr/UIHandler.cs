using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIHandler : MonoBehaviour {
	public Text debugText;
	public InputField input;
	public Button syncButton;
	public Button resetButton;

	public bool debugMode = false;
	public string comPort;

	public SerialComms comms;
		
	void Start() {
		input.contentType = InputField.ContentType.Alphanumeric;
		input.lineType = InputField.LineType.SingleLine;
		input.text = "COM10";

		if (debugMode)
			debugText.gameObject.SetActive(true);
		else if (!debugMode)
			debugText.gameObject.SetActive(false);

		Button btn = syncButton.GetComponent<Button>();
		btn.onClick.AddListener(StartSync);

		Button resetBtn = resetButton.GetComponent<Button>();
		resetButton.onClick.AddListener(ResetBridge);
	}

	void StartSync() {
		comPort = input.text;
		comms.beginConnect = true;
	}

	void ResetBridge() {
		resetButton.interactable = false;
		comms.bridgeReset = true;
		if (!comms.bridgeReset) {
			resetButton.interactable = true;
		}
	}

	void Update() {
		if (!comms.bridgeReset) {
			resetButton.interactable = true;
		}
	}
}
