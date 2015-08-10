using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Shell;

namespace JuliaSet {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    public MainWindow() {
      InitializeComponent();
    }

    DerivableIterFunc fn = new MandelbrotIterFunc();
    LiveDEMIterator iter;
    Visualizer vis;
    Binding iterWidthBinding, iterHeightBinding, visSourceBinding;

    private void Window_Loaded(object sender, RoutedEventArgs e) {
      iter = new LiveDEMIterator(fn, 2000, 1e5);
      vis = new Visualizer(iter);

      iterWidthBinding = new Binding("ActualWidth") {
        Source = ImgGrid,
        Mode = BindingMode.OneWay,
      };

      iterHeightBinding = new Binding("ActualHeight") {
        Source = ImgGrid,
        Mode = BindingMode.OneWay,
      };

      visSourceBinding = new Binding("Source") {
        Source = vis,
        Mode = BindingMode.OneWay,
      };

      BindingOperations.SetBinding(iter, Iterator.WidthProperty, iterWidthBinding);
      BindingOperations.SetBinding(iter, Iterator.HeightProperty, iterHeightBinding);
      BindingOperations.SetBinding(Img, Image.SourceProperty, visSourceBinding);

      iter.CenterX = 0;
      iter.CenterY = 0;
      iter.Scale = 1;

#if true
      vis.Palette = new[] {
          Colors.LightSeaGreen,
          Colors.LightSkyBlue,
          Colors.DarkSlateBlue,
          Colors.DarkOrchid,
          Colors.Firebrick,
          Colors.Crimson,
          Colors.Gold,
          Colors.PaleGreen,
          Colors.ForestGreen,
        };

      vis.LiveColor = Colors.Black;
      vis.PaletteType = VisPaletteType.Relative;
      vis.Scale = 1;
      vis.Offset = 0;
#else
#if false
      vis.Palette = new[] {
          Colors.Firebrick,
          Colors.Gold,
          //Colors.Linen,
          Colors.FloralWhite,
        };
#else
      vis.Palette = new[] {
        Colors.MidnightBlue,
        Colors.DarkSlateBlue,
        Colors.SlateBlue,
        Colors.Lavender,
        Colors.GhostWhite,
      };
#endif
      vis.LiveColor = Colors.Black;
      vis.PaletteType = VisPaletteType.Relative;
      vis.Scale = 1;
      vis.Offset = 0;
#endif

      iter.Resized += delegate (int width, int height, long length) {
        MinProgress.IsIndeterminate =
          MaxProgress.IsIndeterminate = true;

        Progress.Visibility = Visibility.Visible;
        Palette.Visibility = Visibility.Hidden;

        TaskbarInfo.ProgressState = TaskbarItemProgressState.Indeterminate;
      };

      iter.Iterated += async delegate (Iterator sender2, IteratedEventArgs e2) {
        try {
          await Dispatcher.BeginInvoke(new Action(delegate () {
            this.Title = String.Format("{1}Iteration {0} ({2:000.00}%); Center ({3:E6}, {4:E6}); Zoom {5}",
              e2.Current + 1,
              e2.IsDone ? "[Done] " : "",
              Math.Floor(e2.Progress * 10000) / 100,
              iter.CenterX,
              iter.CenterY,
              exp);

            MinProgress.IsIndeterminate =
              MaxProgress.IsIndeterminate = false;
            MinProgress.Value = Math.Min(e2.PixelProgress, e2.IterProgress);
            MaxProgress.Value = Math.Max(e2.PixelProgress, e2.IterProgress);

            if (e2.IsDone) {
              Progress.Visibility = Visibility.Hidden;
              Palette.Visibility = Visibility.Visible;
            }

            TaskbarInfo.ProgressState = e2.IsDone ? TaskbarItemProgressState.None : TaskbarItemProgressState.Normal;
            TaskbarInfo.ProgressValue = e2.Progress;
          }));
        }
        catch (TaskCanceledException) { }
      };

      TaskbarInfo.ProgressState = TaskbarItemProgressState.Indeterminate;

      iter.Start();

      PaletteImg.Source = vis.DrawGradient((int)PaletteGrid.ActualWidth, (int)PaletteGrid.ActualHeight);
    }

    long exp = 0;

    private void Window_MouseWheel(object sender, MouseWheelEventArgs e) {

      exp += e.Delta / 120;
      double newScale = Math.Pow(.9, exp),
             deltaScl = iter.ScalePx - iter.GetScalePx(newScale);

      Point mousePos = Mouse.GetPosition(Img);

      iter.CenterX += (mousePos.X - iter.Width / 2) * deltaScl;
      iter.CenterY += (mousePos.Y - iter.Height / 2) * deltaScl;

      iter.Scale = newScale;
    }

    private void Thumb_DragDelta(object sender, DragDeltaEventArgs e) {
      iter.CenterX -= e.HorizontalChange * iter.ScalePx;
      iter.CenterY -= e.VerticalChange * iter.ScalePx;

      Canvas.SetLeft(Thumb, Canvas.GetLeft(Thumb) + e.HorizontalChange);
      Canvas.SetTop(Thumb, Canvas.GetTop(Thumb) + e.VerticalChange);
    }

    private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e) {
      Canvas.SetLeft(Thumb, 0);
      Canvas.SetTop(Thumb, 0);
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
      if (vis != null)
        PaletteImg.Source = vis.DrawGradient((int)PaletteGrid.ActualWidth, (int)PaletteGrid.ActualHeight);
    }
  }
}
