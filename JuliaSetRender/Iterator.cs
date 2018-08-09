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
      public ManualResetEventSlim SyncEvent { get; }
      public ManualResetEventSlim JoinEvent { get; }
      public int ThreadIndex { get; }

      public WorkerClosure(ManualResetEventSlim syncEvent, ManualResetEventSlim joinEvent, int threadIndex) {
        SyncEvent = syncEvent;
        JoinEvent = joinEvent;
        ThreadIndex = threadIndex;
      }
    }

    List<Thread> threads = new List<Thread>();
    List<ManualResetEventSlim> syncEvents = new List<ManualResetEventSlim>(), joinEvents = new List<ManualResetEventSlim>();
    WriteableBitmap output;
    DerivableIterFunc func;
    Complex center;
    object nextIdxLock = new object();
    double thresh, scale = 1, scalePx, ctrX = 0, ctrY = 0, offsX, offsY, colorScale = 1.0;
    double? min, max;
    int maxIters, width, height, length, currIdx, splCount;
    double[,,] magBuf;
    bool[] rowDrawn;
    bool[,,] populated;
    int[] rowPop;
    byte[] rowBuf;

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

    public double Scale {
      get { return scale; }
      set { scale = value; Resize(); }
    }

    public double ColorScale {
      get { return colorScale; }
      set { colorScale = value; Resize(); }
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

    public double CenterX {
      get { return ctrX; }
      set { ctrX = value; Resize(); }
    }

    public double CenterY {
      get { return ctrY; }
      set { ctrY = value; Resize(); }
    }

    public double OffsX {
      get { return offsX; }
    }

    public double OffsY {
      get { return offsY; }
    }

    public Iterator(WriteableBitmap output, DerivableIterFunc func, double thresh, int maxIters) {
      this.output = output;
      this.func = func;
      this.thresh = thresh;
      this.maxIters = maxIters;

      for (int i = 0; i < Environment.ProcessorCount; i++) {
        ManualResetEventSlim syncEvent = new ManualResetEventSlim(false),
          joinEvent = new ManualResetEventSlim(true);

        syncEvents.Add(syncEvent);
        joinEvents.Add(joinEvent);

        Thread thread = new Thread(Worker) {
          IsBackground = true,
          Name = $"JuliaSetRender Iterator Thread {i + 1}",
          Priority = i == 0 ? ThreadPriority.AboveNormal : ThreadPriority.Highest,
        };

        threads.Add(thread);

        thread.Start(new WorkerClosure(syncEvent, joinEvent, i));
      }
    }

    void Resize() {
      length = width * height;

      magBuf = new double[width, height, splCount * splCount];
      rowDrawn = new bool[height];
      populated = new bool[width, height, splCount * splCount];
      rowPop = new int[height];
      rowBuf = new byte[width * 4];

      scalePx = scale * 2 / Math.Min(width, height);

      offsX = (ctrX / scalePx) - (width / 2);
      offsY = (ctrY / scalePx) - (height / 2);
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

    double ExpSq(double x) {
      return Math.Exp(-x * x);
    }

    double ExpSq(double x, double zeroY, double zeroX) {
      zeroX = zeroY / zeroX;
      zeroY = ExpSq(zeroY);
      x = (ExpSq(x * zeroX) - zeroY) / (1 - zeroY);
      if (x < 0) return 0;
      return x;
    }

    bool TryDrawRow(int y) {
      if (rowDrawn[y]) return false;
      int count = width * splCount * splCount;
      if (rowPop[y] < count || (y < height - 1 && rowPop[y + 1] < count)) return false;

      rowDrawn[y] = true;

      for (int x = 0; x < width; x++) {
        double val = 0, total = 0;


        for (int offY = false && y > 0 ? -1 : 0; offY <= (false && y < height - 1 ? 1 : 0); offY++) {
          for (int offX = false && x > 0 ? -1 : 0; offX <= (false && x < width - 1 ? 1 : 0); offX++) {
            for (int splIdx = 0; splIdx < splCount * splCount; splIdx++) {
              double weight = Math.Max(0, ExpSq(Math.Sqrt(Math.Pow(.5 - offX - SampleX(splIdx), 2) + Math.Pow(.5 - offY - SampleY(splIdx), 2)), 1, 1.5));

              if (populated[x + offX, y + offY, splIdx])
                val += Math.Exp(-Math.Pow(magBuf[x + offX, y + offY, splIdx] * colorScale, .25)) * weight;

              total += weight;
            }
          }
        }

        val /= total;

        int idx = x * 4;

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

      return true;
    }

    void PutPixel(int x, int y, int splIdx, double mag) {
      lock (magBuf) {
        if (mag > 0) {
          magBuf[x, y, splIdx] = mag;
          populated[x, y, splIdx] = true;

          if (!min.HasValue || mag < min) min = mag;
          if (!max.HasValue || mag > max) max = mag;
        }
        else {
          magBuf[x, y, splIdx] = mag;
          populated[x, y, splIdx] = false;
        }

        if (rowPop[y] >= 0) rowPop[y]++;

        TryDrawRow(y);
        if (y > 0) TryDrawRow(y - 1);
      }
    }

    double SampleX(int splNum) {
      return ((splNum % splCount) + .5) / splCount - .5;
    }

    double SampleY(int splNum) {
      return ((splNum / splCount) + .5) / splCount - .5;
    }

    void Worker(object arg) {
      WorkerClosure closure;

      {
        WorkerClosure? refClosure = arg as WorkerClosure?;
        if (!refClosure.HasValue) throw new InvalidOperationException("Worker started without valid WorkerClosure");

        closure = refClosure.Value;
      }

      while (true) {
        closure.SyncEvent.Wait();

        int idx;

        while ((idx = GetNextIndex()) != -1) {
          int x = idx % width,
            y = idx / width;

          for (int splIdx = 0; splIdx < splCount * splCount; splIdx++) {
            int iters = 0;

            Complex z0 = new Complex(
              (x + SampleX(splIdx) + offsX) * scalePx,
              (y + SampleY(splIdx) + offsY) * scalePx),
              z, dz = 1, zNext;

            z = 0; // TODO: uhhhh
            // z = z0;

            double mag = z.Magnitude;

            while (iters < 2000) {
              zNext = func.Invoke(z, z0);
              mag = z.Magnitude;

              if (mag >= 1e5) break;

              dz = func.Delta(z, dz);
              z = zNext;

              iters++;
            }

            PutPixel(x, y, splIdx, mag < 1e5 ? -1 : mag * Math.Log(mag) / dz.Magnitude);
          }
        }

        closure.SyncEvent.Reset();
        closure.JoinEvent.Set();
      }
    }

    public void Start() {
      foreach (ManualResetEventSlim evt in syncEvents)
        evt.Set();

      foreach (ManualResetEventSlim evt in joinEvents)
        evt.Reset();
    }

    public async Task Join() {
      await Task.Run(() => {
        foreach (ManualResetEventSlim evt in joinEvents) evt.Wait();
      });
    }
  }
}
