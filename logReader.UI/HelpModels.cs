namespace logReader.UI
{
    internal enum HelpHeadingLevel { H1 = 1, H2 = 2, H3 = 3 }

    internal enum HelpCalloutKind { None, Tip, Important }

    internal abstract record HelpBlock;

    internal sealed record HelpHeading(HelpHeadingLevel Level, string Text) : HelpBlock;

    internal sealed record HelpParagraph(string Text) : HelpBlock;

    internal sealed record HelpBullet(string Text) : HelpBlock;

    internal sealed record HelpLabeledItem(string Label, string Text, HelpCalloutKind Kind = HelpCalloutKind.None) : HelpBlock;

    internal sealed record HelpExample(string Text) : HelpBlock;

    internal sealed record HelpTopic(string Id, string? ParentId, string Title, IReadOnlyList<HelpBlock> Blocks);
}
