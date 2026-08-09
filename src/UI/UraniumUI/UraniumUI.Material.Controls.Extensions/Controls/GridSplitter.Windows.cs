//namespace UraniumUI.Material.Controls;

//#if WINDOWS
//using Microsoft.UI.Xaml;
//using Microsoft.UI.Xaml.Input;
//using PlatformView = Microsoft.Maui.Platform.ContentPanel;

//namespace UraniumUI.Material.Controls;

//public partial class GridSplitter
//{
//    private PlatformView? _platformView;

//    partial void AttachPlatformEvents()
//    {
//        DetachPlatformEvents();

//        if (Handler?.PlatformView is PlatformView view)
//        {
//            _platformView = view;
//            _platformView.PointerPressed += OnPointerPressed;
//            _platformView.PointerMoved += OnPointerMoved;
//            _platformView.PointerReleased += OnPointerReleased;
//            _platformView.PointerCanceled += OnPointerReleased;
//        }
//    }

//    partial void DetachPlatformEvents()
//    {
//        if (_platformView == null)
//            return;

//        _platformView.PointerPressed -= OnPointerPressed;
//        _platformView.PointerMoved -= OnPointerMoved;
//        _platformView.PointerReleased -= OnPointerReleased;
//        _platformView.PointerCanceled -= OnPointerReleased;
//        _platformView = null;
//    }

//    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
//    {
//        if (sender is not UIElement element)
//            return;

//        var point = e.GetCurrentPoint(element);
//        if (!point.Properties.IsLeftButtonPressed)
//            return;

//        BeginResize(point.Position.X);
//        element.CapturePointer(e.Pointer);
//        e.Handled = true;
//    }

//    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
//    {
//        if (!_dragging || sender is not UIElement element)
//            return;

//        var point = e.GetCurrentPoint(element);
//        UpdateResize(point.Position.X);
//        e.Handled = true;
//    }

//    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
//    {
//        if (!_dragging || sender is not UIElement element)
//            return;

//        EndResize();
//        element.ReleasePointerCapture(e.Pointer);
//        e.Handled = true;
//    }
//}
//#endif


