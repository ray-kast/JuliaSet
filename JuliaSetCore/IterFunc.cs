using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace JuliaSetRender {
  public abstract class IterFunc {
    protected static readonly double GoldenRatio = (1 + Math.Sqrt(5)) / 2;

    public abstract Complex Invoke(Complex z, Complex z0);
  }

  public abstract class DerivableIterFunc : IterFunc {
    public abstract Complex Delta(Complex z, Complex dz);
  }

  public abstract class IterFuncC : IterFunc {
    protected readonly Complex c;

    public IterFuncC(Complex c) {
      this.c = c;
    }
  }

  public abstract class DerivableIterFuncC : DerivableIterFunc {
    protected readonly Complex c;

    public DerivableIterFuncC(Complex c) {
      this.c = c;
    }
  }

  //f(z) = z^2 + c
  public class ZQuadIterFunc : DerivableIterFuncC {
    public ZQuadIterFunc(Complex c) : base(c) { }

    public ZQuadIterFunc() : this(new Complex(-.8, .156)) { }

    public static ZQuadIterFunc Preset1() {
      return new ZQuadIterFunc(new Complex(1 - GoldenRatio, 0));
    }

    public static ZQuadIterFunc Preset2() {
      return new ZQuadIterFunc(new Complex(GoldenRatio - 2, GoldenRatio - 1));
    }

    public static ZQuadIterFunc Preset3() {
      return new ZQuadIterFunc(new Complex(.285, 0));
    }

    public static ZQuadIterFunc Preset4() {
      return new ZQuadIterFunc(new Complex(.285, .01));
    }

    public static ZQuadIterFunc Preset5() {
      return new ZQuadIterFunc(new Complex(.45, .1428));
    }

    public static ZQuadIterFunc Preset6() {
      return new ZQuadIterFunc(new Complex(-.70176, -.3842));
    }

    public static ZQuadIterFunc Preset7() {
      return new ZQuadIterFunc(new Complex(-.835, .2321));
    }

    public override Complex Invoke(Complex z, Complex z0) {
      return z * z + c;
    }

    public override Complex Delta(Complex z, Complex dz) {
      return 2 * z * dz;
    }
  }

  //f(z) = z^n + c
  public class ZPowIterFunc : DerivableIterFuncC {
    protected readonly double pow;

    public ZPowIterFunc(Complex c, double pow)
      : base(c) {
      this.pow = pow;
    }

    public ZPowIterFunc() : this(new Complex(0, 0), 3) { }

    public override Complex Invoke(Complex z, Complex z0) {
      return Complex.Pow(z, pow) + c;
    }

    public override Complex Delta(Complex z, Complex dz) {
      return pow * Complex.Pow(z, pow - 1) * dz;
    }
  }

  //f(z) = exp z^3 + c
  public class ExpZCubIterFunc : DerivableIterFuncC {
    public ExpZCubIterFunc(Complex c) : base(c) { }

    public ExpZCubIterFunc() : this(new Complex(-.621, 0)) { }

    public override Complex Invoke(Complex z, Complex z0) {
      return Complex.Exp(Complex.Pow(z, 3)) + c;
    }

    public override Complex Delta(Complex z, Complex dz) {
      return 3 * Complex.Exp(Complex.Pow(z, 3)) * Complex.Pow(z, 2) * dz;
    }
  }

  //f(z) = z exp z + c
  public class ZExpZIterFunc : IterFuncC {
    public ZExpZIterFunc(Complex c) : base(c) { }

    public ZExpZIterFunc() : this(new Complex(.04, 0)) { }

    public override Complex Invoke(Complex z, Complex z0) {
      return z * Complex.Exp(z) + c;
    }
  }

  //f(z) = z^2 exp z + c
  public class ZQuadExpZIterFunc : IterFunc {
    readonly Complex c;

    public ZQuadExpZIterFunc(Complex c) {
      this.c = c;
    }

    public ZQuadExpZIterFunc() : this(new Complex(.21, 0)) { }

    public override Complex Invoke(Complex z, Complex z0) {
      return z * z * Complex.Exp(z) + c;
    }
  }

  //f(z) = sqrt(sin(z^2)) + c
  public class SqrtSinhZQuadIterFunc : IterFunc {
    readonly Complex c;

    public SqrtSinhZQuadIterFunc(Complex c) {
      this.c = c;
    }

    public SqrtSinhZQuadIterFunc() : this(new Complex(.065, .122)) { }

    public override Complex Invoke(Complex z, Complex z0) {
      return Complex.Sqrt(Complex.Sinh(z * z)) + c;
    }
  }

  //f(z) = (z^2 + z) / ln z + c
  public class ZQuadPlusZDivLnZIterFunc : IterFunc {
    readonly Complex c;

    public ZQuadPlusZDivLnZIterFunc(Complex c) {
      this.c = c;
    }

    public ZQuadPlusZDivLnZIterFunc() : this(new Complex(.268, .06)) { }

    public override Complex Invoke(Complex z, Complex z0) {
      return (z * z + z) / Complex.Log(z) + c;
    }
  }

  //f(z) = z^2 + z0
  public class MandelbrotIterFunc : DerivableIterFunc {
    public override Complex Invoke(Complex z, Complex z0) {
      return z * z + z0;
    }

    public override Complex Delta(Complex z, Complex dz) {
      return 2 * z * dz + 1;
    }
  }

  public class BurningShipIterFunc : DerivableIterFunc {
    Complex AbsComps(Complex z) => new Complex(Math.Abs(z.Real), Math.Abs(z.Imaginary));

    Complex DeltaAbsComps(Complex z, Complex dz) => new Complex(z.Real < 0 ? -dz.Real : dz.Real, z.Imaginary < 0 ? -dz.Imaginary : dz.Imaginary);

    //Complex DeltaAbsComps(Complex z, Complex dz) {
    //  return AbsComps(z) - AbsComps(z - dz);
    //}

    public override Complex Invoke(Complex z, Complex z0) {
      return Complex.Pow(AbsComps(z), 2) + z0;
    }

    public override Complex Delta(Complex z, Complex dz) {
      return 2 * AbsComps(z) * DeltaAbsComps(z, dz) + 1;
    }
  }

  public class PolyFracIterFunc1 : DerivableIterFunc {
    readonly Complex c;

    public PolyFracIterFunc1(Complex c) {
      this.c = c;
    }

    public PolyFracIterFunc1() : this(new Complex(0, 0)) { }

    public override Complex Invoke(Complex z, Complex z0) {
      return (1 - Complex.Pow(z, 3) / 6) / Complex.Pow(z - z * z / 2, 2) + c;
    }

    public override Complex Delta(Complex z, Complex dz) {
      return (-z * z * dz / 2 * Complex.Pow(z - (z * z) / 2, 2) + 2 * (1 - Complex.Pow(z, 3) / 6) * dz * (1 - z)) / Complex.Pow(z - z * z / 2, 4);
    }
  }

  public class ExpZPlusZ0IterFunc : IterFunc {
    public override Complex Invoke(Complex z, Complex z0) {
      return Complex.Pow(z, 2) * Complex.Exp(z) + z0;
    }
  }
}
