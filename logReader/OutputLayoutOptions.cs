namespace logReader
{
    // Общие настройки раскладки выходных таблиц (CSV / XLSX).
    public sealed class OutputLayoutOptions
    {
        // Строка с CAN ID посылок над строкой «Шаг»/«Время» и имён параметров. По умолчанию выключена.
        public bool IncludeDeviceIdHeaderRow { get; set; }
    }
}
