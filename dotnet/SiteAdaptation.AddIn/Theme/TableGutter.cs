using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SiteAdaptation.AddIn.Theme
{
    // R403, R406, R376. The header of a table sits outside its list, so the scroll bar of the list takes width from the star columns of
    // the rows and not from the header. The header grid ends with a column named by this property, and that column takes the width the
    // scroll bar takes, none while the bar is not shown, so the columns of header and rows are the same scrolled or not
    public static class TableGutter
    {
        public static readonly DependencyProperty ColumnProperty = DependencyProperty.RegisterAttached("Column", typeof(ColumnDefinition), typeof(TableGutter),
            new PropertyMetadata(null, OnColumn));

        public static ColumnDefinition GetColumn(DependencyObject list)
        {
            return (ColumnDefinition)list.GetValue(ColumnProperty);
        }

        public static void SetColumn(DependencyObject list, ColumnDefinition value)
        {
            list.SetValue(ColumnProperty, value);
        }

        private static void OnColumn(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            var list = (ListBox)target;
            var column = e.NewValue as ColumnDefinition;
            if (column == null)
            {
                return;
            }
            list.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((sender, args) => Follow(list, column)));
            list.SizeChanged += (sender, args) => Follow(list, column);
            list.Loaded += (sender, args) => Follow(list, column);
        }

        // The width of the list the viewer does not use is the scroll bar, as the list has no padding and no border
        public static void Follow(ListBox list, ColumnDefinition column)
        {
            ScrollViewer viewer = Find(list);
            double width = viewer == null ? 0 : Math.Max(0, list.ActualWidth - viewer.ViewportWidth);
            if (Math.Abs(column.Width.Value - width) > 0.01)
            {
                column.Width = new GridLength(width);
            }
        }

        private static ScrollViewer Find(DependencyObject root)
        {
            for (int at = 0; at < VisualTreeHelper.GetChildrenCount(root); at++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, at);
                ScrollViewer viewer = child as ScrollViewer ?? Find(child);
                if (viewer != null)
                {
                    return viewer;
                }
            }
            return null;
        }
    }
}
