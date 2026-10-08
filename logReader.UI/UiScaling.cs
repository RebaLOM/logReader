namespace logReader.UI
{
    internal static class UiScaling
    {
        // Формы, собранные в коде, размечены в пикселях для 96 DPI. Масштаб применяется после
        // добавления всех контролов: иначе ранний layout масштабирует только часть из них.
        public static void Apply(ContainerControl form)
        {
            form.AutoScaleDimensions = new SizeF(96F, 96F);
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.PerformAutoScale();
        }
    }
}
