using UnityEngine;
using UnityEngine.UI;

public class SFB_DemoControl_v2 : MonoBehaviour {

	public GameObject cameraObject;							// The camera object
	public float mouseSensitivityY = -0.005f;
	public float mouseSensitivityX = 0.01f;

	private Vector2 lastMousePosition;

	public Slider heightBar;

	public GameObject canvas;
	public Button superRandomButton;

	/// <summary>
	/// Simply sets the .y value of the camera transform
	/// </summary>
	/// <param name="newValue">New value.</param>
	public void SetCameraHeight(float newValue)
	{
		//Debug.Log("Old:" + cameraObject.transform.localPosition);
		Vector3 newPosition = new Vector3(cameraObject.transform.localPosition.x, newValue, cameraObject.transform.localPosition.z);
		cameraObject.transform.localPosition = newPosition;			// Set the position
		//Debug.Log("New: " + cameraObject.transform.localPosition);
	}

	/// <summary>
	/// Simply sets the timescale
	/// </summary>
	/// <param name="newValue">New value.</param>
	public void SetTimescale(float newValue){
		Time.timeScale = newValue;							// Set the value
	}

	private void FixedUpdate()
	{
		if (InfinityPBR.Demo.InfinityDemoInput.RightMousePressed)
		{
			lastMousePosition = InfinityPBR.Demo.InfinityDemoInput.MousePosition;
		}
		if (InfinityPBR.Demo.InfinityDemoInput.RightMouseHeld)
		{
			//Debug.Log("Value 1: " + heightBar.value);
			heightBar.value += (InfinityPBR.Demo.InfinityDemoInput.MousePosition.y - lastMousePosition.y) * mouseSensitivityY;
			lastMousePosition = InfinityPBR.Demo.InfinityDemoInput.MousePosition;
			//Debug.Log("Value 2: " + heightBar.value);
		}
	}

	public void Update()
	{
		if (InfinityPBR.Demo.InfinityDemoInput.IsPressedThisFrame(UnityEngine.InputSystem.Key.C))
		{
			canvas.SetActive(!canvas.activeSelf);
		}
		
		if (InfinityPBR.Demo.InfinityDemoInput.IsPressedThisFrame(UnityEngine.InputSystem.Key.R))
		{
			superRandomButton.onClick.Invoke();
		}
	}
}
