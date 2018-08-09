using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace JuliaSetRender {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    public MainWindow() {
      InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e) {
      //WriteableBitmap bmp = new WriteableBitmap(5120, 2880, 96, 96, PixelFormats.Bgra32, null),
      WriteableBitmap bmp = new WriteableBitmap(2560, 1440, 96, 96, PixelFormats.Bgra32, null),
        outImg = new WriteableBitmap(bmp);
      Iterator iter = new Iterator(bmp, new MandelbrotIterFunc(), 1, 2000000) {
        SampleCount = 4,
        CenterX = -3.020050e-2,
        CenterY = -6.522111e-1,
        Scale = Math.Pow(.9, 54),
        ColorScale = 1e5,
      };

      //outImg.Freeze();

      OutputImg.Source = bmp;

      byte[] pxs = new byte[bmp.PixelWidth * bmp.PixelHeight * 4];

      for (int i = 0; i + 3 < pxs.Length; i += 4) {
        pxs[i] = 0;
        pxs[i + 1] = 0;
        pxs[i + 2] = 0;
        pxs[i + 3] = 255;
      }

      bmp.WritePixels(new Int32Rect(0, 0, bmp.PixelWidth, bmp.PixelHeight), pxs, bmp.PixelWidth * 4, 0);

      iter.Width = bmp.PixelWidth;
      iter.Height = bmp.PixelHeight;

      iter.Start();

      await iter.Join();

      SaveFileDialog dlg = new SaveFileDialog() {
        Title = "Save Image...",
        AddExtension = true,
        Filter = "PNG Files|*.png",
      };

      if (dlg.ShowDialog() ?? false) {
        using (var fs = new FileStream(dlg.FileName, FileMode.Create)) {
          PngBitmapEncoder enc = new PngBitmapEncoder();

          enc.Frames.Add(BitmapFrame.Create(bmp));
          enc.Save(fs);
        }
      }
    }
  }
}
