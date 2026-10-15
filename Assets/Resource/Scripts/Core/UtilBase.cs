// ============================================================
// UtilBase -- base class for utility classes.
// Conventions:
//   1) Util is a pure static class with no state and no fields.
//   2) Util only performs calculations and data transforms; it never mutates Model.
//   3) Util can be called by any Control / View.
//   4) Util must never dispatch events through EventCenter (avoids implicit coupling).
// ============================================================
public abstract class UtilBase
{
        // Prevent instantiation.
    protected UtilBase() { }
}
