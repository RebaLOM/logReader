using System.Runtime.CompilerServices;

namespace logReader.UI.Helpers;

internal static class WindowSizing
{
    private static readonly ConditionalWeakTable<Form, SizingState> States = new();

    public static void Prepare(Form form) => States.GetValue(form, owner => new SizingState(owner));

    private sealed class SizingState
    {
        private readonly Form form;
        private Size? minimumAt96Dpi;
        private bool fitting;

        public SizingState(Form owner)
        {
            form = owner;
            form.Shown += (_, _) => Fit();
            form.Resize += (_, _) =>
            {
                if (minimumAt96Dpi != null && form.Visible) Fit();
            };
            form.DpiChanged += (_, _) =>
            {
                if (form.IsHandleCreated && !form.IsDisposed)
                    form.BeginInvoke((Action)Fit);
            };
        }

        private void Fit()
        {
            if (fitting || form.IsDisposed || form.WindowState != FormWindowState.Normal) return;
            fitting = true;
            try
            {
                if (minimumAt96Dpi == null)
                {
                    // Initial autoscaling is complete when the form is first shown.
                    float scale = Math.Max(1, form.DeviceDpi) / 96f;
                    minimumAt96Dpi = new Size((int)Math.Round(form.MinimumSize.Width / scale),
                        (int)Math.Round(form.MinimumSize.Height / scale));
                }
                Size baseline = minimumAt96Dpi.Value;
                var work = Screen.FromControl(form).WorkingArea;
                int inset = Math.Min(UiScale.Px(form, 8), Math.Min(work.Width, work.Height) / 8);
                var available = new Size(Math.Max(1, work.Width - 2 * inset), Math.Max(1, work.Height - 2 * inset));
                // Keep actions reachable on a scaled Full HD screen; page content can scroll.
                form.MinimumSize = new Size(Math.Min(available.Width, UiScale.Px(form, baseline.Width)),
                    Math.Min(available.Height, UiScale.Px(form, baseline.Height)));
                if (!form.MaximumSize.IsEmpty &&
                    (form.MaximumSize.Width > available.Width || form.MaximumSize.Height > available.Height))
                    form.MaximumSize = available;
                form.Size = new Size(Math.Min(form.Width, available.Width), Math.Min(form.Height, available.Height));
                form.Location = new Point(Math.Clamp(form.Left, work.Left + inset, work.Right - inset - form.Width),
                    Math.Clamp(form.Top, work.Top + inset, work.Bottom - inset - form.Height));
            }
            finally
            {
                fitting = false;
            }
        }
    }
}
