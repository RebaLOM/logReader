using System.Runtime.CompilerServices;

namespace logReader.UI.Helpers;

internal static class WindowSizing
{
    private static readonly ConditionalWeakTable<Form, SizingState> States = new();

    public static void Prepare(Form form) => States.GetValue(form, owner => new SizingState(owner));

    private sealed class SizingState
    {
        private readonly Form form;
        private readonly ResizeCoordinator resize;
        private Size? minimumAt96Dpi;
        private Size maximumAt96Dpi;
        private FormWindowState previousWindowState;
        private bool fitting;

        public SizingState(Form owner)
        {
            form = owner;
            resize = new ResizeCoordinator(owner, Fit);
            previousWindowState = form.WindowState;
            form.Shown += (_, _) =>
            {
                CaptureBaseline();
                resize.Request();
                resize.Flush();
            };
            form.Resize += (_, _) =>
            {
                bool restored = previousWindowState != FormWindowState.Normal && form.WindowState == FormWindowState.Normal;
                previousWindowState = form.WindowState;
                if (restored && minimumAt96Dpi != null && form.Visible) resize.Request();
            };
            form.ResizeEnd += (_, _) =>
            {
                if (minimumAt96Dpi == null) return;
                resize.Request();
                resize.Flush();
            };
            form.DpiChanged += (_, _) => { if (minimumAt96Dpi != null) resize.Request(); };
        }

        private void CaptureBaseline()
        {
            if (minimumAt96Dpi != null) return;
            // Initial autoscaling is complete even when the form is first shown maximized.
            float scale = Math.Max(1, form.DeviceDpi) / 96f;
            minimumAt96Dpi = new Size((int)Math.Round(form.MinimumSize.Width / scale),
                (int)Math.Round(form.MinimumSize.Height / scale));
            maximumAt96Dpi = new Size((int)Math.Round(form.MaximumSize.Width / scale),
                (int)Math.Round(form.MaximumSize.Height / scale));
        }

        private void Fit()
        {
            if (fitting || form.IsDisposed || form.Disposing || minimumAt96Dpi == null ||
                form.WindowState != FormWindowState.Normal) return;
            fitting = true;
            try
            {
                Size baseline = minimumAt96Dpi.Value;
                var work = Screen.FromControl(form).WorkingArea;
                int inset = Math.Min(UiScale.Px(form, 8), Math.Min(work.Width, work.Height) / 8);
                var available = new Size(Math.Max(1, work.Width - 2 * inset), Math.Max(1, work.Height - 2 * inset));
                // Keep actions reachable on a scaled Full HD screen; page content can scroll.
                var minimum = new Size(Math.Min(available.Width, UiScale.Px(form, baseline.Width)),
                    Math.Min(available.Height, UiScale.Px(form, baseline.Height)));
                if (form.MinimumSize != minimum) form.MinimumSize = minimum;
                // A zero maximum dimension stays unrestricted; a temporary small screen
                // must not replace the form's original constraint on a larger screen.
                var maximum = new Size(maximumAt96Dpi.Width == 0 ? 0 : Math.Min(available.Width, UiScale.Px(form, maximumAt96Dpi.Width)),
                    maximumAt96Dpi.Height == 0 ? 0 : Math.Min(available.Height, UiScale.Px(form, maximumAt96Dpi.Height)));
                if (form.MaximumSize != maximum) form.MaximumSize = maximum;
                var size = new Size(Math.Min(form.Width, available.Width), Math.Min(form.Height, available.Height));
                if (form.Size != size) form.Size = size;
                var location = new Point(Math.Clamp(form.Left, work.Left + inset, Math.Max(work.Left + inset, work.Right - inset - form.Width)),
                    Math.Clamp(form.Top, work.Top + inset, Math.Max(work.Top + inset, work.Bottom - inset - form.Height)));
                if (form.Location != location) form.Location = location;
            }
            finally
            {
                fitting = false;
            }
        }
    }
}
