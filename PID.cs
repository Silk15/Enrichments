using UnityEngine;

public class PID
{
    public float kP, kI, kD;

    private Rigidbody rigidbody;
    private Vector3 positionIntegral;
    private Vector3 lastPositionError;

    private Vector3 rotationIntegral;
    private Vector3 lastRotationError;

    public PID(Rigidbody rigidbody, float kP, float kI, float kD)
    {
        this.rigidbody = rigidbody;
        this.kP = kP;
        this.kI = kI;
        this.kD = kD;
        Reset();
    }

    public void Reset()
    {
        positionIntegral = Vector3.zero;
        lastPositionError = Vector3.zero;
        rotationIntegral = Vector3.zero;
        lastRotationError = Vector3.zero;
    }

    public void MoveTo(Vector3 targetPos, Quaternion targetRot, float maxForce = 1000f, float maxTorque = 1000f, float iLimit = 5f)
    {
        float dt = Time.fixedDeltaTime;

        Vector3 posErr = targetPos - rigidbody.position;
        positionIntegral = Vector3.ClampMagnitude(positionIntegral + posErr * dt, iLimit);
        Vector3 posDeriv = (posErr - lastPositionError) / dt;
        lastPositionError = posErr;
        rigidbody.AddForce(Vector3.ClampMagnitude(kP * posErr + kI * positionIntegral + kD * posDeriv, maxForce), ForceMode.Force);

        Quaternion rotErrQuat = targetRot * Quaternion.Inverse(rigidbody.rotation);
        rotErrQuat.ToAngleAxis(out float angle, out Vector3 axis);
        if (angle > 180f) angle -= 360f;
        if (axis == Vector3.zero || float.IsNaN(axis.x)) axis = Vector3.up;
        Vector3 rotErr = axis.normalized * Mathf.Deg2Rad * angle;
        rotationIntegral = Vector3.ClampMagnitude(rotationIntegral + rotErr * dt, iLimit);
        Vector3 rotDeriv = (rotErr - lastRotationError) / dt;
        lastRotationError = rotErr;
        rigidbody.AddTorque(Vector3.ClampMagnitude(kP * rotErr + kI * rotationIntegral + kD * rotDeriv, maxTorque), ForceMode.Force);
    }
}