using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace JuliaSetRender {
  class Iterator {
    struct WorkerClosure {
      public ManualResetEvent SyncEvent { get; }
      public int ThreadIndex { get; }

      public WorkerClosure(ManualResetEvent syncEvent, int threadIndex) {
        SyncEvent = syncEvent;
        ThreadIndex = threadIndex;
      }
    }

    List<Thread> threads = new List<Thread>();
    List<ManualResetEvent> syncEvents = new List<ManualResetEvent>();
    WriteableBitmap output;
    IterFunc func;
    object nextIdxLock = new object();
    double thresh;
    int maxIters, width, height, length, currIdx;
    double[,,] magBuf;
    bool[,,] populated;
    int[] rowPop;
    byte[] rowBuf;
    private int splCount;

    public int Width {
      get {
        return width;
      }

      set {
        width = value;
        Resize();
      }
    }

    public int Height {
      get {
        return height;
      }

      set {
        height = value;
        Resize();
      }
    }

    public int SampleCount {
      get {
        return splCount;
      }
      set {
        splCount = value;

        Resize();
      }
    }

    public Iterator(WriteableBitmap output, IterFunc func, double thresh, int maxIters) {
      this.output = output;
      this.func = func;
      this.thresh = thresh;
      this.maxIters = maxIters;

      for (int i = 0; i < Environment.ProcessorCount; i++) {
        ManualResetEvent syncEvent = new ManualResetEvent(false);

        syncEvents.Add(syncEvent);

        Thread thread = new Thread(Worker) {
          IsBackground = true,
          Name = $"JuliaSetRender Iterator Thread {i + 1}",
          Priority = ThreadPriority.AboveNormal,
        };

        threads.Add(thread);

        thread.Start(new WorkerClosure(syncEvent, i));
      }
    }

    void Resize() {
      length = width * height;

      magBuf = new double[width, height, splCount * splCount];
      populated = new bool[width, height, splCount * splCount];
      rowPop = new int[height];
      rowBuf = new byte[width * 4];
    }

    void Resize(int width, int height) {
      this.width = width;
      this.height = height;

      Resize();
    }

    int GetNextIndex() {
      lock (nextIdxLock) {
        int ret = currIdx;

        if (currIdx >= 0) {
          if (currIdx < length - 1) currIdx++;
          else currIdx = -1;
        }

        return ret;
      }
    }

    void PutPixel(int x, int y, int splIdx, double mag) {
      lock (magBuf) {
        if (mag > 0) {
          magBuf[x, y, splIdx] = mag;
          populated[x, y, splIdx] = true;
        }
        else {
          magBuf[x, y, splIdx] = mag;
          populated[x, y, splIdx] = false;
        }

        rowPop[y]++;

        if (rowPop[y] >= width * splCount * splCount) {
          for (int i = 0; i < width; i++) {
            double val = 0, total = 0;


            for (int offY = y > 0 ? -1 : 0; offY <= (y < height - 1 ? 1 : 0); offY++) {
              for (int offX = i == 0 ? 0 : -1; offX <= (i < width - 1 ? 1 : 0); offX++) {
                for (int spIdx = 0; spIdx < splCount * splCount; spIdx++) {
                  double weight = Math.Max(0, .5 - Math.Sqrt(Math.Pow(.5 - offX - SampleX(spIdx), 2) + Math.Pow(.5 - offY - SampleY(spIdx), 2)));

                  if (populated[i + offX, y + offY, spIdx])
                    val += Math.Exp(-Math.Pow(magBuf[i + offX, y + offY, spIdx], .25)) * weight;

                  total += weight;
                }
              }
            }

            val /= total;

            int idx = i * 4;

            rowBuf[idx] =
              rowBuf[idx + 1] =
              rowBuf[idx + 2] = (byte)Math.Round(255 * val);
            rowBuf[idx + 3] = 255;
          }

          try {
            output.Dispatcher.Invoke(() => {
              output.WritePixels(new Int32Rect(0, y, width, 1), rowBuf, width * 4, 0);
            });
          }
          catch (TaskCanceledException) { }
        }
      }
    }

    double SampleX(int splNum) {
      return ((splNum % splCount) + .5) / splCount;
    }

    double SampleY(int splNum) {
      return ((splNum / splCount) + .5) / splCount;
    }

    void Worker(object arg) {
      WorkerClosure closure;

      {
        WorkerClosure? refClosure = arg as WorkerClosure?;
        if (!refClosure.HasValue) throw new InvalidOperationException("Worker started without valid WorkerClosure");

        closure = refClosure.Value;
      }

      while (true) {
        closure.SyncEvent.WaitOne();

        int idx;
        DerivableIterFunc fn = new MandelbrotIterFunc();
        double scaleX = 3, scaleY = 3;

        if (width > height)
          scaleX *= (double)width / height;
        else
          scaleY *= (double)height / width;

        while ((idx = GetNextIndex()) != -1) {
          int x = idx % width,
            y = idx / width;

          for (int splIdx = 0; splIdx < splCount * splCount; splIdx++) {
            int iters = 0;

            Complex z = new Complex(
              ((x + SampleX(splIdx)) / width - .5) * scaleX,
              ((y + SampleY(splIdx)) / height - .5) * scaleY),
              z0 = z, dz = 1, zNext;

            double mag = z.Magnitude;

            while (iters < 2000) {
              zNext = fn.Invoke(z, z0);
              mag = z.Magnitude;

              if (mag >= 1e5) break;

              dz = fn.Delta(z, dz);
              z = zNext;

              iters++;
            }

            PutPixel(x, y, splIdx, mag < 1e5 ? -1 : mag * Math.Log(mag) / dz.Magnitude);
          }
        }

        closure.SyncEvent.Reset();
      }
    }

    public void Start() {
      foreach (ManualResetEvent evt in syncEvents)
        evt.Set();
    }
  }
}
