using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace JuliaSet {
  enum VisPaletteType {
    Relative,
    CyclicWhole,
    CyclicSingle
  }

  struct FloatColor {
    float[] comps;

    public float[] Comps {
      get { return comps; }
    }

    public float B {
      get { return comps[0]; }
      set { comps[0] = value; }
    }

    public float G {
      get { return comps[1]; }
      set { comps[1] = value; }
    }

    public float R {
      get { return comps[2]; }
      set { comps[2] = value; }
    }

    public float A {
      get { return comps[3]; }
      set { comps[3] = value; }
    }

    public FloatColor(float a, float r, float g, float b) {
      comps = new float[] { b, g, r, a };
    }
  }

  delegate void VisVisualizeEvent(Visualizer vis);

  class Visualizer : INotifyPropertyChanged {
    const int bpp = 4;
    const double dpiX = 96, dpiY = 96;

    Thread visThread;
    AutoResetEvent startEvent = new AutoResetEvent(false);
    Iterator iter;

    readonly object queueLock = new object();

    int width, height, stride;
    long length, byteLen;
    double offs, scale;
    bool isDone;
    double[] result;
    bool[] isAlive;
    FloatColor[] palette;
    FloatColor liveClr;
    VisPaletteType paletteType;

    BitmapSource src;

    event PropertyChangedEventHandler propChanged;

    event VisVisualizeEvent visualized;

    public Color[] Palette {
      set {
        lock (queueLock) palette = (from color in value
                                    select new FloatColor(color.ScA, color.ScR, color.ScG, color.ScB)).ToArray();
      }
    }

    public Color LiveColor {
      set {
        lock (queueLock) liveClr = new FloatColor(value.ScA, value.ScR, value.ScG, value.ScB);
      }
    }

    public VisPaletteType PaletteType {
      get { return paletteType; }
      set { lock (queueLock) paletteType = value; }
    }

    public double Offset {
      get { return offs; }
      set { lock (queueLock) offs = value; }
    }

    public double Scale {
      get { return scale; }
      set { lock (queueLock) scale = value; }
    }

    public BitmapSource Source {
      get { return src; }
      private set {
        if (src != value) {
          src = value;
          NotifyPropertyChanged("Source");
        }
      }
    }

    public event PropertyChangedEventHandler PropertyChanged {
      add { propChanged += value; }
      remove { propChanged -= value; }
    }

    public event VisVisualizeEvent Visualized {
      add { visualized += value; }
      remove { visualized -= value; }
    }

    public Visualizer(Iterator iter) {
      this.iter = iter;

      visThread = new Thread(Visualize);
      visThread.IsBackground = true;
      visThread.Name = "JuliaSet Visualizer Thread";
      visThread.Priority = ThreadPriority.Highest;

      iter.Started += IterStarted;
      iter.Iterated += Iterated;

      if (iter.IsRunning) Start();
    }

    void NotifyPropertyChanged(string name) {
      if (propChanged != null) {
        propChanged(this, new PropertyChangedEventArgs(name));
      }
    }

    void Start() {
      visThread.Start();
    }

    void IterStarted(Iterator iter) {
      Start();
    }

    void Iterated(Iterator sender, IteratedEventArgs e) {
      lock (queueLock) {
        result = sender.Result.ToArray();
        isAlive = sender.IsAlive.ToArray();
        width = e.Width;
        height = e.Height;
        length = e.Length;
        isDone = e.IsDone;
        stride = width * bpp;
        byteLen = length * bpp;

        startEvent.Set();
      }
    }

    FloatColor GetColor(FloatColor[] palette, FloatColor fallback, double pos) {
      double wrap = pos % palette.Length;
      if (wrap < 0) wrap = (wrap + palette.Length) % palette.Length;

      if (double.IsNaN(pos)) return fallback;
      else if (double.IsPositiveInfinity(pos)) return palette[palette.Length - 1];
      else if (double.IsNegativeInfinity(pos)) return palette[0];

      if (wrap < 0) wrap += palette.Length;

      int floor = (int)Math.Floor(wrap),
          ceil = (int)Math.Ceiling(pos) % palette.Length;

      if (floor == ceil) return palette[floor];
      else if (ceil - floor > 1) return fallback;

      float pct = (float)(wrap - floor), invPct;

      pct = (3 - 2 * pct) * pct * pct;

      invPct = 1f - pct;

      FloatColor floorClr = palette[floor],
        ceilClr = palette[ceil];

      return new FloatColor(
          floorClr.A * invPct + ceilClr.A * pct,
          floorClr.R * invPct + ceilClr.R * pct,
          floorClr.G * invPct + ceilClr.G * pct,
          floorClr.B * invPct + ceilClr.B * pct);
    }

    void Visualize() {
      int width, height, stride;
      long length, byteLen = 0, j;
      double palOffs, palScale;
      VisPaletteType palType;
      bool isDone, doRepop;
      FloatColor liveClr, currClr;

      double[] result;
      bool[] isAlive;
      FloatColor[] palette;
      byte[] bytes = new byte[0];

      BitmapSource src;

      while (true) {
        startEvent.WaitOne();

        lock (queueLock) {
          width = this.width;
          height = this.height;
          length = this.length;
          stride = this.stride;
          result = this.result;
          isAlive = this.isAlive;
          isDone = this.isDone;
          palette = this.palette;
          liveClr = this.liveClr;
          palType = this.paletteType;
          palOffs = this.offs;
          palScale = this.scale;

          doRepop = this.byteLen != byteLen;
          byteLen = this.byteLen;
        }

        if (width == 0 || height == 0) continue;

        if (doRepop)
          bytes = new byte[byteLen];


        double min, max, offs = isDone ? palOffs : 0, scale;
        bool isPalRel = palType == VisPaletteType.Relative || !isDone;

        if (isPalRel) {

          min = double.MaxValue;
          max = double.MinValue;

          for (long i = 1; i < length; i++) {
            if (!isAlive[i]) {
              min = Math.Min(min, result[i]);
              max = Math.Max(max, result[i]);
            }
          }

          offs -= min;
          scale = (isDone ? palScale * (palette.Length - 1) : 255) / (max - min);
        }
        else scale = (palType == VisPaletteType.CyclicSingle ? 1 : palette.Length) / palScale;

        if (isDone) {
          for (long i = 0; i < length; i++) {
            j = i * bpp;

            currClr = isAlive[i] ? liveClr : isPalRel ? GetColor(palette, liveClr, (result[i] + offs) * scale) : GetColor(palette, liveClr, Math.Log(result[i] + 1) * scale);

            bytes[j] = (byte)(currClr.B * 255);
            bytes[j + 1] = (byte)(currClr.G * 255);
            bytes[j + 2] = (byte)(currClr.R * 255);
            bytes[j + 3] = (byte)(currClr.A * 255);
          }
        }
        else {
          for (long i = 0; i < length; i++) {
            j = i * bpp;

            bytes[j] =
              bytes[j + 1] =
              bytes[j + 2] = (byte)(isAlive[i] ? 0 : (result[i] + offs) * scale);
            bytes[j + 3] = (byte)255;
          }
        }

        src = BitmapSource.Create(width, height, dpiX, dpiY, PixelFormats.Bgra32, null, bytes, stride);

        Source = src.GetAsFrozen() as BitmapSource;

        if (visualized != null) visualized(this);
      }
    }

    public BitmapSource DrawGradient(int width, int height) {
      long length = width * height, currByte;
      FloatColor currClr;

      byte[] bytes = new byte[length * bpp];

      for(int row = 0; row < height; row++) {
        for(int col = 0; col < width; col++) {
          currByte = (row * width + col) * bpp;
          currClr = GetColor(palette, liveClr, (double)col * (palette.Length - 1) / width);

          bytes[currByte] = (byte)(currClr.B * 255);
          bytes[currByte + 1] = (byte)(currClr.G * 255);
          bytes[currByte + 2] = (byte)(currClr.R * 255);
          bytes[currByte + 3] = (byte)(currClr.A * 255);
        }
      }
      
      return BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bytes, width * bpp);
    }
  }
}
