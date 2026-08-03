namespace GymOS.Domain.Attendance;

// Real door-access/biometric check-in methods (RFID, fingerprint, face) plug in later behind
// IDoorAccessProvider/IBiometricCheckInProvider - this enum only covers the MVP's simulated paths.
public enum AttendanceMethod
{
    QrSimulated,
    Manual
}
