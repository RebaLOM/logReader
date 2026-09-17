namespace logReader
{
    // Общие настройки раскладки выходных таблиц (CSV / XLSX).
    public sealed class OutputLayoutOptions
    {
        // Первая строка с CAN ID посылок над именами параметров. По умолчанию выключена.
        public bool IncludeDeviceIdHeaderRow { get; set; }
    }
}
