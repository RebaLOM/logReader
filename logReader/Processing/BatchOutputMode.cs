namespace logReader.Processing
{
    // Режим пакетной обработки папки с логами.
    public enum BatchOutputMode
    {
        PerInputFile = 0,
        MergeToSingleFile = 1,
        SplitTrcByDate = 2,
    }
}
