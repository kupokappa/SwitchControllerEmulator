using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputManager : MonoBehaviour {
	public KeyCode SW_A { get; set; }
	public KeyCode SW_B { get; set; }
	public KeyCode SW_X { get; set; }
	public KeyCode SW_Y { get; set; }

	public KeyCode SW_L { get; set; }
	public KeyCode SW_R { get; set; }
	public KeyCode SW_ZL { get; set; }
	public KeyCode SW_ZR { get; set; }

	public KeyCode SW_PL { get; set; }
	public KeyCode SW_MN { get; set; }
	public KeyCode SW_CP { get; set; }
	public KeyCode SW_HM { get; set; }

	public KeyCode SW_LC { get; set; }
	public KeyCode SW_RC { get; set; }

	public KeyCode SW_D_U { get; set; }
	public KeyCode SW_D_D { get; set; }
	public KeyCode SW_D_L { get; set; }
	public KeyCode SW_D_R { get; set; }

	public KeyCode SW_LS_U { get; set; }
	public KeyCode SW_LS_D { get; set; }
	public KeyCode SW_LS_L { get; set; }
	public KeyCode SW_LS_R { get; set; }

	//these will be mouse inputs and i'm stupid
	public KeyCode SW_RS_U { get; set; }
	public KeyCode SW_RS_D { get; set; }
	public KeyCode SW_RS_L { get; set; }
	public KeyCode SW_RS_R { get; set; }

	void Awake() {
		SW_A = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_A", "R"));
		SW_B = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_B", "Space"));
		SW_X = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_X", "Tab"));
		SW_Y = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_Y", "F"));

		SW_L = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_L", "E"));
		SW_R = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_R", "Mouse1"));
		SW_ZL = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_ZL", "LeftControl"));
		SW_ZR = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_ZR", "Mouse0"));

		SW_PL = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_PL", "Alpha3"));
		SW_MN = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_MN", "Alpha1"));
		SW_CP = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_CP", "Alpha2"));
		SW_HM = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_HM", "Escape"));

		SW_LC = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_LC", "T"));
		SW_RC = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_RC", "Q"));

		SW_D_U = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_D_U", "UpArrow"));
		SW_D_D = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_D_D", "DownArrow"));
		SW_D_L = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_D_L", "LeftArrow"));
		SW_D_R = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_D_R", "RightArrow"));

		SW_LS_U = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_LS_U", "W"));
		SW_LS_D = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_LS_D", "S"));
		SW_LS_L = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_LS_L", "A"));
		SW_LS_R = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_LS_R", "D"));

		//these will be mouse inputs and i'm stupid
		/*SW_RS_U = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_RS_U", "R"));
		SW_RS_D = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_RS_D", "R"));
		SW_RS_L = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_RS_L", "R"));
		SW_RS_R = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("SW_RS_R", "R"));*/
	}

	void Start() {

	}

	void Update() {
		
	}
}
