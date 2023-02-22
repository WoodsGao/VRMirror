using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class VRMirror : MonoBehaviour
{

    private Camera _camera;
    private Transform _mirror;
    void OnEnable()
    {
        _mirror = transform.parent;
        _camera = GetComponent<Camera>();
    }

    // Update is called once per frame
    void Update()
    {
        if (_camera == null || Camera.main == null) return;
        Transform mainCameraTrans = Camera.main.transform;
        Vector3 normalDir = _mirror.rotation * Vector3.back;
        // check distance
        float distance = Vector3.Dot(normalDir, mainCameraTrans.position - _mirror.position);
        if (distance <= 0 || distance > 100)
        {
            _camera.enabled = false;
        }
        else
        {
            _camera.enabled = true;
        }

        transform.position = mainCameraTrans.position - 2 * distance * normalDir;

        Vector3 forwardDir = mainCameraTrans.rotation * Vector3.forward;
        forwardDir = forwardDir - 2 * Vector3.Dot(normalDir, forwardDir) * normalDir;

        Vector3 upDir = mainCameraTrans.rotation * Vector3.up;
        upDir = upDir - 2 * Vector3.Dot(normalDir, upDir) * normalDir;

        transform.rotation = Quaternion.LookRotation(forwardDir, upDir);

        _camera.fieldOfView = Camera.main.fieldOfView;
        _camera.ResetWorldToCameraMatrix();
        _camera.ResetProjectionMatrix();
        // _camera.worldToCameraMatrix = Matrix4x4.Scale(new Vector3(-1, 1, 1)) * _camera.worldToCameraMatrix;
        _camera.projectionMatrix = Matrix4x4.Scale(new Vector3(-1, 1, -1)) * Matrix4x4.Perspective(_camera.fieldOfView, _camera.aspect, _camera.nearClipPlane, _camera.farClipPlane);
        _camera.cullingMatrix = _camera.projectionMatrix * _camera.worldToCameraMatrix;
        // if (_camera.stereoEnabled)
        // {
        // Debug.Log("Stereo");
        // Matrix4x4 viewL = _camera.worldToCameraMatrix;
        // Matrix4x4 viewR = _camera.worldToCameraMatrix;
        // viewL[12] += 1f;

        // _camera.SetStereoViewMatrix(Camera.StereoscopicEye.Left, viewL);
        // _camera.SetStereoViewMatrix(Camera.StereoscopicEye.Right, viewR);
        // }
        GL.invertCulling = false;

    }

}
