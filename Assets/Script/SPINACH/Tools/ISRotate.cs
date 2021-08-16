using UnityEngine;

[ExecuteInEditMode]
public class ISRotate : MonoBehaviour {

	public bool executeInEdit;
	public Transform target;
	public Vector3 dir;

	void Update () {
		if(!Application.isPlaying && !executeInEdit) return;
		target.Rotate(dir * Time.deltaTime);
	}
}
