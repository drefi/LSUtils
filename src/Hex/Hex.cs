using System;
using System.Diagnostics.CodeAnalysis;

namespace LSUtils.Hex {
    public struct Hex {
        public int Q { get; }
        public int R { get; }
        public int S { get; }
        public Hex(int q, int r) {
            Q = q;
            R = r;
            S = -q - r;
        }
        public override bool Equals([NotNullWhen(true)] object? obj) {
            if (obj is not Hex) return false;
            Hex other = (Hex)obj;
            return other.R == R && other.S == S && other.Q == Q;
        }

        public static bool operator ==(Hex left, Hex right) {
            return left.Equals(right);
        }

        public static bool operator !=(Hex left, Hex right) {
            return !(left == right);
        }

        public override int GetHashCode() {
            return Q ^ R ^ S;
        }

    }
    public static class HexExtensions {
        public static Hex Add(this Hex a, Hex b) {
            return new Hex(a.Q + b.Q, a.R + b.R);
        }
        public static Hex Subtract(this Hex a, Hex b) {
            return new Hex(a.Q - b.Q, a.R - b.R);
        }
        public static int Length(this Hex hex) {
            return (Math.Abs(hex.Q) + Math.Abs(hex.R) + Math.Abs(hex.S) / 2);
        }
        public static int Distance(this Hex a, Hex b) {
            return (Length(Subtract(a, b)));
        }
    }
}
