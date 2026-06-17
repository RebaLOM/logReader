namespace logReader.UI
{
    // Режим пакетной обработки папки с .trc-логами.
    internal enum BatchOutputMode
    {
        PerInputFile = 0,
        MergeTrcToSingleFile = 1,
        SplitTrcByDate = 2,
    }
}
