using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JuliaSet {
  class LiveDEMIterator : SingleThreadedIterator<DerivableIterFunc> {
    AutoResetEvent startEvent = new AutoResetEvent(false);

    Complex[] points, origPoints, deltas;

    readonly object sizeLock = new object();

    public LiveDEMIterator(DerivableIterFunc func, long iters, double thresh)
      : base(func, iters, thresh) {
    }

    public override void Start() {
      startEvent.Set();

      base.Start();
    }

    protected override void Resize() {
      lock (sizeLock) {
        base.Resize();

        startEvent.Set();
      }
    }

    protected override void DoIterations() {
      int width = 0, height = 0;
      long i = 0, length = 0, numDead = 0;
      double scalePx, offsX, offsY, mag;
      bool areAnyAlive, didAnyDie;
      Complex zNew;

      while (true) {
        startEvent.WaitOne();

        do {
          if (doRepop) {
            lock (sizeLock) {
              width = this.width;
              height = this.height;
              length = this.length;
              scalePx = this.scalePx;
              offsX = this.offsX;
              offsY = this.offsY;

              result = new double[length];
              points = new Complex[length];
              origPoints = new Complex[length];
              deltas = new Complex[length];
              isAlive = new bool[length];

              areAnyAlive =
                didAnyDie = false;

              numDead = 0;

              long j;
              for (int row = 0; row < height; row++) {
                for (int col = 0; col < width; col++) {
                  j = row * width + col;

                  mag = Complex.Abs(
                    points[j] = origPoints[j] = new Complex((col + offsX) * scalePx, (row + offsY) * scalePx));

                  deltas[j] = 1;


                  isAlive[j] = mag < thresh;

                  if (isAlive[j]) areAnyAlive = true;
                  else {
                    result[j] = mag * Math.Log(mag);
                    didAnyDie = true;
                    numDead++;
                  }
                }
              }

              doRepop = false;
              i = 0;

              if (iterated != null) iterated(this, new IteratedEventArgs(width, height, length, 0, areAnyAlive ? 0 : 1, areAnyAlive, didAnyDie, !areAnyAlive));
            }
          }
          else {
            areAnyAlive =
              didAnyDie = false;

            for (long j = 0; j < length; j++) {
              if (isAlive[j]) {
                zNew = func.Invoke(points[j], origPoints[j]);

                deltas[j] = func.Delta(points[j], deltas[j]);

                mag = Complex.Abs(points[j] = zNew);

                if (mag < thresh) {
                  areAnyAlive = true;
                }
                else {
                  isAlive[j] = false;
                  result[j] = Math.Exp(-Math.Pow(mag * Math.Log(mag) / Complex.Abs(deltas[j]), .45));
                  didAnyDie = true;
                  numDead++;
                }
              }
            }

            if (iterated != null) iterated(this, new IteratedEventArgs(width, height, length, i, Math.Max((double)i / iters, (double)numDead / length), areAnyAlive, didAnyDie, (i >= iters - 1) || !areAnyAlive));

            i++;
          }
        } while (i < iters && areAnyAlive);
      }
    }
  }
}
