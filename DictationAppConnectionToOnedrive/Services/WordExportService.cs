using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DictationApp.Models;
using System.IO;
using Color = DocumentFormat.OpenXml.Wordprocessing.Color;

namespace DictationApp.Services
{
    public static class WordExportService
    {
        /// <summary>
        /// Exports a transcription to a .docx file.
        /// Returns the path to the generated file.
        /// </summary>
        public static string Export(AudioFile audioFile, string outputPath)
        {
            using var doc = WordprocessingDocument.Create(
                outputPath, WordprocessingDocumentType.Document);

            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Add styles
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            stylesPart.Styles = CreateStyles();

            // ── Title ──────────────────────────────────────────────
            body.AppendChild(CreateHeading(audioFile.FileName, "Heading1"));

            // ── Metadata table ─────────────────────────────────────
            var metaTable = CreateMetadataTable(audioFile);
            body.AppendChild(metaTable);
            body.AppendChild(new Paragraph()); // spacer

            // ── Transcription heading ───────────────────────────────
            body.AppendChild(CreateHeading("Transcription", "Heading2"));

            // ── Transcription text ──────────────────────────────────
            var text = audioFile.Transcription ?? "[No transcription available]";
            foreach (var para in SplitIntoParagraphs(text))
                body.AppendChild(CreateBodyParagraph(para));

            // ── Reviewer notes (if any) ─────────────────────────────
            if (!string.IsNullOrWhiteSpace(audioFile.ReviewerNotes))
            {
                body.AppendChild(new Paragraph()); // spacer
                body.AppendChild(CreateHeading("Reviewer Notes", "Heading2"));
                body.AppendChild(CreateBodyParagraph(audioFile.ReviewerNotes));
            }

            // ── Footer: export info ─────────────────────────────────
            body.AppendChild(new Paragraph()); // spacer
            body.AppendChild(CreateBodyParagraph(
                $"Exported: {DateTime.Now:dddd, MMMM d, yyyy 'at' h:mm tt}  |  " +
                $"Duration: {audioFile.DurationDisplay}",
                italic: true, color: "888888", size: 18));

            // Page margins
            var sectPr = new SectionProperties();
            sectPr.AppendChild(new PageMargin
            {
                Top = 1440, Bottom = 1440, Left = 1440, Right = 1440
            });
            body.AppendChild(sectPr);

            mainPart.Document.Save();
            return outputPath;
        }

        private static Paragraph CreateHeading(string text, string styleId)
        {
            var para = new Paragraph();
            var ppr = new ParagraphProperties();
            ppr.AppendChild(new ParagraphStyleId { Val = styleId });
            para.AppendChild(ppr);
            var run = new Run();
            run.AppendChild(new Text(text));
            para.AppendChild(run);
            return para;
        }

        private static Paragraph CreateBodyParagraph(
            string text,
            bool bold = false,
            bool italic = false,
            string? color = null,
            int size = 22)
        {
            var para = new Paragraph();
            var ppr = new ParagraphProperties();
            ppr.AppendChild(new SpacingBetweenLines { After = "120", Line = "276", LineRule = LineSpacingRuleValues.Auto });
            para.AppendChild(ppr);

            var run = new Run();
            var rpr = new RunProperties();
            rpr.AppendChild(new FontSize { Val = size.ToString() });
            rpr.AppendChild(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
            if (bold)  rpr.AppendChild(new Bold());
            if (italic) rpr.AppendChild(new Italic());
            if (color != null) rpr.AppendChild(new Color { Val = color });
            run.AppendChild(rpr);
            run.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(run);
            return para;
        }

        private static Table CreateMetadataTable(AudioFile file)
        {
            var table = new Table();

            var tblPr = new TableProperties();
            tblPr.AppendChild(new TableBorders(
                new TopBorder    { Val = BorderValues.Single, Size = 4, Color = "DADCE0" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "DADCE0" },
                new LeftBorder   { Val = BorderValues.Single, Size = 4, Color = "DADCE0" },
                new RightBorder  { Val = BorderValues.Single, Size = 4, Color = "DADCE0" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "DADCE0" },
                new InsideVerticalBorder   { Val = BorderValues.Single, Size = 4, Color = "DADCE0" }
            ));
            tblPr.AppendChild(new TableWidth { Width = "9360", Type = TableWidthUnitValues.Dxa });
            table.AppendChild(tblPr);

            var rows = new[]
            {
                ("Recorded",   file.RecordedAt.ToString("dddd, MMMM d, yyyy 'at' h:mm tt")),
                ("Duration",   file.DurationDisplay),
                ("File Size",  file.FileSizeDisplay),
                ("Status",     file.StatusDisplay
                               .Replace("🎙","").Replace("📤","").Replace("📝","")
                               .Replace("✅","").Replace("📄","").Trim()),
            };

            foreach (var (label, value) in rows)
                table.AppendChild(CreateMetaRow(label, value));

            return table;
        }

        private static TableRow CreateMetaRow(string label, string value)
        {
            var row = new TableRow();

            // Label cell
            var labelCell = new TableCell();
            var lcPr = new TableCellProperties();
            lcPr.AppendChild(new TableCellWidth { Width = "1800", Type = TableWidthUnitValues.Dxa });
            lcPr.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "F1F3F4" });
            labelCell.AppendChild(lcPr);
            var lp = new Paragraph();
            var lr = new Run();
            var lrPr = new RunProperties();
            lrPr.AppendChild(new Bold());
            lrPr.AppendChild(new FontSize { Val = "20" });
            lrPr.AppendChild(new RunFonts { Ascii = "Calibri" });
            lrPr.AppendChild(new Color { Val = "5F6368" });
            lr.AppendChild(lrPr);
            lr.AppendChild(new Text(label));
            lp.AppendChild(lr);
            labelCell.AppendChild(lp);
            row.AppendChild(labelCell);

            // Value cell
            var valueCell = new TableCell();
            var vcPr = new TableCellProperties();
            vcPr.AppendChild(new TableCellWidth { Width = "7560", Type = TableWidthUnitValues.Dxa });
            valueCell.AppendChild(vcPr);
            var vp = new Paragraph();
            var vr = new Run();
            var vrPr = new RunProperties();
            vrPr.AppendChild(new FontSize { Val = "20" });
            vrPr.AppendChild(new RunFonts { Ascii = "Calibri" });
            vr.AppendChild(vrPr);
            vr.AppendChild(new Text(value));
            vp.AppendChild(vr);
            valueCell.AppendChild(vp);
            row.AppendChild(valueCell);

            return row;
        }

        private static IEnumerable<string> SplitIntoParagraphs(string text) =>
            text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Replace("\r\n", " ").Replace("\n", " ").Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));

        private static Styles CreateStyles()
        {
            var styles = new Styles();

            // Default style
            var docDefaults = new DocDefaults();
            var rDefault = new RunPropertiesDefault();
            var rPr = new RunPropertiesBaseStyle();
            rPr.AppendChild(new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri" });
            rPr.AppendChild(new FontSize { Val = "22" });
            rDefault.AppendChild(rPr);
            docDefaults.AppendChild(rDefault);
            styles.AppendChild(docDefaults);

            // Heading 1
            var h1 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading1" };
            h1.AppendChild(new StyleName { Val = "heading 1" });
            var h1Pr = new StyleRunProperties();
            h1Pr.AppendChild(new Bold());
            h1Pr.AppendChild(new FontSize { Val = "36" });
            h1Pr.AppendChild(new RunFonts { Ascii = "Calibri" });
            h1Pr.AppendChild(new Color { Val = "1A73E8" });
            h1.AppendChild(h1Pr);
            var h1PPr = new StyleParagraphProperties();
            h1PPr.AppendChild(new SpacingBetweenLines { Before = "0", After = "200" });
            h1.AppendChild(h1PPr);
            styles.AppendChild(h1);

            // Heading 2
            var h2 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading2" };
            h2.AppendChild(new StyleName { Val = "heading 2" });
            var h2Pr = new StyleRunProperties();
            h2Pr.AppendChild(new Bold());
            h2Pr.AppendChild(new FontSize { Val = "28" });
            h2Pr.AppendChild(new RunFonts { Ascii = "Calibri" });
            h2Pr.AppendChild(new Color { Val = "202124" });
            h2.AppendChild(h2Pr);
            var h2PPr = new StyleParagraphProperties();
            h2PPr.AppendChild(new SpacingBetweenLines { Before = "280", After = "120" });
            h2.AppendChild(h2PPr);
            styles.AppendChild(h2);

            return styles;
        }
    }
}
