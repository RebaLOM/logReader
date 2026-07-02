namespace logReader.UI
{
    // Режим пакетной обработки папки с логами.
    internal enum BatchOutputMode
    {
        PerInputFile = 0,
        MergeToSingleFile = 1,
        SplitTrcByDate = 2,
    }
}
