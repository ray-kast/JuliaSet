using JuliaSetRender;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace JuliaSet
{
  class LiveDEMIterator : SingleThreadedIterator<DerivableIterFunc>
  {
    AutoResetEvent startEvent = new AutoResetEvent(false);

    Complex[] points, origPoints, deltas;

    readonly object sizeLock = new object();

    public LiveDEMIterator(DerivableIterFunc func, long iters, double thresh, IterSeedMode seedMode, Visual sourceVis)
      : base(func, iters, thresh, seedMode, sourceVis) {
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
      int bWidth = 0, bHeight = 0, iWidth = 0, iHeight = 0, spls = 0;
      long i = 0, length = 0, numDead = 0;
      double scalePx, offsX, offsY, mag;
      bool areAnyAlive, didAnyDie;
      Complex zNew;

      while (true) {
        startEvent.WaitOne();

        do {
          if (doRepop) {
            lock (sizeLock) {
              bWidth = this.bWidth;
              bHeight = this.bHeight;
              iWidth = this.iWidth;
              iHeight = this.iHeight;
              spls = this.spls;
              length = bLength;
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
              for (int row = 0; row < bHeight; row++) {
                for (int col = 0; col < bWidth; col++) {
                  j = row * bWidth + col;

                  origPoints[j] = new Complex((col + offsX) * scaleSpl, (row + offsY) * scaleSpl);

                  switch (seedMode) {
                    case IterSeedMode.Zero:
                      points[j] = 0;
                      break;
                    case IterSeedMode.Coordinate:
                      points[j] = origPoints[j];
                      break;
                  }

                  mag = points[j].Magnitude;

                  switch (seedMode) {
                    case IterSeedMode.Coordinate: deltas[j] = 1; break;
                    case IterSeedMode.Zero: deltas[j] = 0; break;
                  }

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

              double pixelProg = (double)numDead / length;

              iterated?.Invoke(this, new IteratedEventArgs(
                bWidth,
                bHeight,
                iWidth,
                iHeight,
                spls,
                length,
                0,
                pixelProg,
                pixelProg,
                0,
                areAnyAlive,
                didAnyDie,
                !areAnyAlive));
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
                  //result[j] = mag * Math.Log(mag) / Complex.Abs(deltas[j]);
                  result[j] = Math.Exp(-Math.Pow(mag * Math.Log(mag) / Complex.Abs(deltas[j]), .5));
                  didAnyDie = true;
                  numDead++;
                }
              }
            }

            double pixelProg = (double)numDead / length,
              iterProg = (double)i / iters;

            iterated?.Invoke(this, new IteratedEventArgs(
              bWidth,
              bHeight,
              iWidth,
              iHeight,
              spls,
              length,
              i,
              Math.Max(pixelProg, iterProg),
              pixelProg,
              iterProg,
              areAnyAlive,
              didAnyDie,
              (i >= iters - 1) || !areAnyAlive));

            i++;
          }
        } while (i < iters && areAnyAlive);
      }
    }
  }
}
