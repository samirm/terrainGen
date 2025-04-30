using Unity.Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Tooltip("An empty GameObject the camera follows and orbits")]
    public Transform focusTarget; // Assign an empty GameObject here

    [Header("Movement Settings")] public float panSpeed = 10f;

    public float zoomSpeed = 5f;
    public float rotationSpeed = 50f;
    public float followOffsetSmoothing = 0.5f; // How quickly the camera snaps to target

    [Header("Camera Control")] public float startingDistance = 20f; // Initial distance from target

    public float minDistance = 5f;
    public float maxDistance = 50f;
    public float rotationSmoothing = 0.1f; // Smoothing for camera orbit
    public float zoomSmoothing = 0.2f; // Smoothing for camera zoom 

    [Header("References")] [Tooltip("The Cinemachine Virtual Camera controlling the view")]
    public CinemachineCamera virtualCamera;

    private CameraInputActions _cameraActions;
    private CinemachineComponentBase _cameraBodyComponent;
    private CinemachineFollow _cameraFollow;
    private float _currentDistance; // Current distance from focus target
    private float _currentDistanceVelocity;

    // Camera State
    private float _currentHeading; // Horizontal orbit angle (controlled by Q/E)
    private float _currentHeadingVelocity;
    private Vector3 _currentVelocity = Vector3.zero; // For SmoothDamp
    private Vector2 _panInput;
    private float _rotateInput;
    private float _sphereRadius;
    private float _targetDistance;
    private float _targetHeading;
    private Vector3 _targetPosition; // Desired position of the focusTarget
    private float _zoomInput;

    private void Awake()
    {
        _cameraActions = new CameraInputActions();

        if (virtualCamera == null)
        {
            Debug.LogError("Cinemachine Camera missing!");
            enabled = false;
            return;
        }

        if (focusTarget == null)
        {
            Debug.LogError("Focus Target missing!");
            enabled = false;
            return;
        }

        // Get CinemachineFollow Component
        _cameraFollow = virtualCamera.GetCinemachineComponent(CinemachineCore.Stage.Body) as CinemachineFollow;
        if (_cameraFollow == null)
        {
            Debug.LogError("Cinemachine Camera needs a CinemachineFollow component in the Body stage!", this);
            enabled = false;
            return;
        }

        // Initial State Calculation
        _targetPosition = focusTarget.position.normalized * _sphereRadius;
        focusTarget.position = _targetPosition;
        var initialOffset = virtualCamera.transform.position - focusTarget.position;
        _currentDistance = initialOffset.magnitude;
        _targetDistance = Mathf.Clamp(_currentDistance, minDistance, maxDistance);
        var targetUp = focusTarget.position.normalized;
        var initialOffsetOnPlane = Vector3.ProjectOnPlane(initialOffset, targetUp);
        if (initialOffsetOnPlane.sqrMagnitude > 0.01f)
        {
            var targetRight = Vector3.Cross(targetUp, Vector3.up);
            if (targetRight.sqrMagnitude < 0.001f) targetRight = Vector3.Cross(targetUp, Vector3.forward);
            targetRight.Normalize();
            _currentHeading = Vector3.SignedAngle(targetRight, initialOffsetOnPlane.normalized, targetUp);
        }

        _targetHeading = _currentHeading;


        // Configure CinemachineFollow defaults (Optional but clarifies intent)
        // Set offset to zero because *our script* calculates the offset/position.
        _cameraFollow.FollowOffset = Vector3.zero;
        // *** NO DAMPING SETTINGS NEEDED HERE ***

        // Get sphere radius logic (if separate from initial state calc)
        var generator = GetComponentInParent<HexSphereGenerator>();
        if (generator != null)
            _sphereRadius = generator.radius;
        else
            Debug.LogWarning("HexasphereGenerator not found, using default sphere radius.");
    }

    private void LateUpdate() // Use LateUpdate for camera calculations AFTER target has moved
    {
        if (virtualCamera is null || _cameraFollow is null || focusTarget is null) return;

        HandleInput(); // Read and process raw input values
        UpdateTargetState(); // Update desired focus position, heading, distance
        ApplyCameraTransform(); // Calculate and set camera position/rotation

        // We are manually controlling the camera's position relative to the target,
        // so the built-in damping/offset of CinemachineFollow might not be used directly.
        // Ensure the VCam LookAt is still set to focusTarget for aiming.
    }

    private void OnEnable()
    {
        _cameraActions.Camera.Enable();
        // ... (Input action subscriptions remain the same) ...
    }

    private void OnDisable()
    {
        _cameraActions.Camera.Disable();
        // ... (Input action unsubscriptions remain the same) ...
    }

    private void HandleInput()
    {
        // Store input values (subscriptions update panInput, zoomInput, rotateInput)
    }


    private void UpdateTargetState()
    {
        // --- Update Focus Target Position (Panning) ---
        if (_panInput.sqrMagnitude > 0.1f)
        {
            var scaledPanSpeed = panSpeed * Time.deltaTime;
            var targetUp = _targetPosition.normalized; // Use desired position's normal

            // Get camera's right relative to the *current* camera transform for intuitive panning
            // Project onto the tangent plane at the target position
            var camRight = Vector3.ProjectOnPlane(virtualCamera.transform.right, targetUp).normalized;
            // Prevent issues near poles if camera right aligns with target up
            if (camRight.sqrMagnitude < 0.001f)
                camRight = Vector3
                    .ProjectOnPlane(Quaternion.AngleAxis(1f, targetUp) * virtualCamera.transform.right, targetUp)
                    .normalized;

            var verticalRotationAxis = camRight;
            var horizontalRotationAxis = Vector3.Cross(targetUp, camRight);

            var horizontalAngle = -_panInput.x * scaledPanSpeed;
            var verticalAngle = _panInput.y * scaledPanSpeed;

            _targetPosition = Quaternion.AngleAxis(horizontalAngle, horizontalRotationAxis) * _targetPosition;
            _targetPosition = Quaternion.AngleAxis(verticalAngle, verticalRotationAxis) * _targetPosition;

            _targetPosition = _targetPosition.normalized * _sphereRadius; // Keep on sphere
        }

        // Smoothly move the actual focus target towards the desired position
        focusTarget.position = Vector3.SmoothDamp(focusTarget.position, _targetPosition, ref _currentVelocity,
            followOffsetSmoothing);
        focusTarget.position = focusTarget.position.normalized * _sphereRadius; // Clamp to sphere
        focusTarget.rotation =
            Quaternion.LookRotation(focusTarget.position.normalized, focusTarget.up); // Orient target


        // --- Update Target Heading (Rotation) ---
        if (Mathf.Abs(_rotateInput) > 0.1f) _targetHeading += _rotateInput * rotationSpeed * Time.deltaTime;

        // --- Update Target Distance (Zoom) ---
        if (Mathf.Abs(_zoomInput) > 0.1f)
        {
            var processedZoom = -_zoomInput * (1f / 120f) * zoomSpeed; // Inverted scroll to zoom direction
            _targetDistance = Mathf.Clamp(_targetDistance + processedZoom, minDistance, maxDistance);
            _zoomInput = 0f; // Consume zoom input
        }
    }


    private void ApplyCameraTransform()
    {
        // Smooth heading and distance towards targets
        _currentHeading =
            Mathf.SmoothDampAngle(_currentHeading, _targetHeading, ref _currentHeadingVelocity, rotationSmoothing);
        _currentDistance =
            Mathf.SmoothDamp(_currentDistance, _targetDistance, ref _currentDistanceVelocity, zoomSmoothing);

        // Calculate desired camera position relative to the *current* focusTarget position
        var targetUp = focusTarget.position.normalized;

        // Start with direction pointing straight out from sphere center through target
        var baseDirection = targetUp;

        // Find a consistent 'right' vector on the tangent plane for heading calculation
        var worldRight = Vector3.right; // Use world X as reference
        var targetRight = Vector3.ProjectOnPlane(worldRight, targetUp).normalized;
        if (targetRight.sqrMagnitude < 0.001f) // Handle poles where world right aligns with up
            targetRight = Vector3.ProjectOnPlane(Vector3.forward, targetUp).normalized;

        // Calculate the final offset direction by rotating around the target's 'up' axis
        var offsetDirection = Quaternion.AngleAxis(_currentHeading, targetUp) * targetRight;

        // Position the camera 'currentDistance' away along this offset direction
        // We are essentially setting the local position relative to the focus target
        var desiredLocalPosition = offsetDirection * _currentDistance;


        // --- IMPORTANT: Update CinemachineFollow Offset ---
        // We tell CinemachineFollow component where the camera should be relative to the target.
        // Convert the desired world offset direction/distance into the component's local offset space.
        // NOTE: This assumes the CinemachineFollow component uses standard damping/offset logic.
        // Since we calculate the final position, we might just directly set the camera's transform
        // OR find the correct way to feed this into the component. Let's try setting offset.

        // The offset is usually in the target's local space IF the target rotates,
        // or world space offset. Since our target's "up" always points out, let's use that.
        // Calculate offset relative to target's orientation might be complex.
        // Let's try setting the world offset based on target position.

        var worldOffset = desiredLocalPosition; // Our calculated offset IS in world space relative to target origin
        // The component might expect offset differently. Testing needed.
        // A common pattern is to set a LOCAL offset and let damping work.

        // **Alternative & Simpler:** Bypass component offset, set VCam position directly
        // (May fight with CM updates - needs testing)
        var desiredWorldPosition = focusTarget.position + worldOffset;
        // Instead of setting virtualCamera.transform.position directly which CM might override,
        // we set the offset on the component responsible for positioning.
        // Try converting world offset to local offset relative to camera's current orientation? This is tricky.
        _cameraFollow.FollowOffset = virtualCamera.transform.InverseTransformDirection(worldOffset);


        // --- Let's Rethink: Control the Camera Directly (potentially fighting CM Brain less) ---
        // Calculate final position
        var finalCameraPosition = focusTarget.position + offsetDirection * _currentDistance;
        // Calculate final rotation (look at target, keep camera 'up' aligned away from sphere center)
        var cameraUp = targetUp; // Camera's up vector should point away from sphere center
        var finalCameraRotation = Quaternion.LookRotation(focusTarget.position - finalCameraPosition, cameraUp);

        // Directly influence the CinemachineCamera's state before the Brain updates.
        // This is advanced usage.
        // virtualCamera.OnTargetObjectWarped(focusTarget, finalCameraPosition - virtualCamera.transform.position); // Doesn't quite fit
        // OR Set transform directly in LateUpdate might work if Brain update order allows
        virtualCamera.transform.position = finalCameraPosition;
        virtualCamera.transform.rotation = finalCameraRotation;


        // Ensure the CinemachineFollow component doesn't fight us too much.
        // Setting its damping to 0 and offset might help, but direct transform control
        // in LateUpdate is often the most direct way when bypassing component logic.
        // Make sure LookAt is still set to focusTarget in the CinemachineCamera component settings.
    }
}