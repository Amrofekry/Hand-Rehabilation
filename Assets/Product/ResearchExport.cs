using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace GripRehab.Product
{
    /// <summary>Explicit export formats. CSV cells cannot execute spreadsheet formulas.</summary>
    public static class ResearchExport
    {
        public static string CsvCell(string value)
        {
            value = value ?? "";
            var trimmed = value.TrimStart();
            if (trimmed.Length > 0 && ("=+-@".IndexOf(trimmed[0]) >= 0 || value[0] == '\t' || value[0] == '\r')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        public static string SessionsCsv(IEnumerable<SessionRecord> sessions)
        {
            var text = new StringBuilder("session_id,started_utc,module,task,state,synthetic,score,duration_seconds,valid_seconds,peak_newtons,mean_closure\r\n");
            foreach (var s in sessions) text.Append(CsvCell(s.id)).Append(',').Append(CsvCell(s.startedUtc)).Append(',')
                .Append(s.module).Append(',').Append(s.task).Append(',').Append(s.state).Append(',').Append(s.synthetic ? "true" : "false").Append(',')
                .Append(s.score).Append(',').Append(Number(s.durationSeconds)).Append(',').Append(Number(s.validSeconds)).Append(',')
                .Append(s.hasForce ? Number(s.peakNewtons) : "").Append(',').Append(Number(s.meanClosure)).Append("\r\n");
            return text.ToString();
        }
        public static string MetricsCsv(SessionRecord session)
        {
            var text = new StringBuilder("session_id,seconds,source,unit,value,valid,synthetic,shape,calibration_revision,hand_closure,speed,jitter\r\n");
            foreach (var m in session.metrics) text.Append(CsvCell(session.id)).Append(',').Append(Number(m.seconds)).Append(',')
                .Append(CsvCell(m.source)).Append(',').Append(m.unit).Append(',').Append(Number(m.value)).Append(',')
                .Append(m.valid ? "true" : "false").Append(',').Append(m.synthetic ? "true" : "false").Append(',')
                .Append(CsvCell(m.shape)).Append(',').Append(CsvCell(m.calibrationRevision)).Append(',')
                .Append(Number(m.handClosure)).Append(',').Append(Number(m.motionSpeed)).Append(',').Append(Number(m.motionJitter)).Append("\r\n");
            return text.ToString();
        }
        static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        static string PdfText(string text)
        {
            var result = new StringBuilder();
            foreach (var c in text) { if (c == '(' || c == ')' || c == '\\') result.Append('\\'); result.Append(c >= 32 && c <= 126 ? c : '?'); }
            return result.ToString();
        }
        /// <summary>Paginated English research report with source badges and score bars, using account IDs.</summary>
        public static byte[] Pdf(string accountId, SessionRecord[] sessions)
        {
            var objects = new List<string> { "", "", "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>" };
            var pages = new List<int>();
            int pageCount = Math.Max(1, (sessions.Length + 13) / 14);
            for (int page = 0; page < pageCount; page++)
            {
                int pageId = objects.Count + 1, contentId = pageId + 1; pages.Add(pageId);
                objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 3 0 R >> >> /Contents " + contentId + " 0 R >>");
                var body = new StringBuilder("0.05 0.12 0.16 rg\nBT /F1 22 Tf 42 796 Td (GripRehab - research session report) Tj ET\n");
                body.Append("BT /F1 10 Tf 42 770 Td (Account: ").Append(PdfText(accountId)).Append(") Tj ET\n")
                    .Append("BT /F1 10 Tf 42 750 Td (Game metrics only. Not a clinical assessment.) Tj ET\n");
                for (int i = page * 14; i < Math.Min(sessions.Length, (page + 1) * 14); i++)
                {
                    var s = sessions[i]; int y = 708 - (i % 14) * 44;
                    string line = s.startedUtc + " | Module " + s.module + " | " + s.state + (s.synthetic ? " | SYNTHETIC" : "");
                    body.Append("BT /F1 9 Tf 42 ").Append(y).Append(" Td (").Append(PdfText(line)).Append(") Tj ET\n")
                        .Append("BT /F1 9 Tf 42 ").Append(y - 15).Append(" Td (Score ").Append(s.score).Append("/100 | Valid ")
                        .Append(Number(s.validSeconds)).Append(" s) Tj ET\n0.08 0.45 0.4 rg 340 ").Append(y - 16).Append(' ')
                        .Append(Number(Math.Max(0, Math.Min(100, s.score)) * 1.7)).Append(" 9 re f\n0.05 0.12 0.16 rg\n");
                }
                body.Append("BT /F1 10 Tf 42 34 Td (Page ").Append(page + 1).Append(" of ").Append(pageCount).Append(") Tj ET\n");
                var content = body.ToString(); objects.Add("<< /Length " + Encoding.ASCII.GetByteCount(content) + " >>\nstream\n" + content + "endstream");
            }
            objects[0] = "<< /Type /Catalog /Pages 2 0 R >>";
            var kids = new StringBuilder(); foreach (var id in pages) kids.Append(id).Append(" 0 R ");
            objects[1] = "<< /Type /Pages /Count " + pages.Count + " /Kids [" + kids + "] >>";
            using (var stream = new MemoryStream())
            {
                Action<string> write = text => { var bytes = Encoding.ASCII.GetBytes(text); stream.Write(bytes, 0, bytes.Length); };
                write("%PDF-1.4\n"); var offsets = new List<long> { 0 };
                for (int i = 0; i < objects.Count; i++) { offsets.Add(stream.Position); write((i + 1) + " 0 obj\n" + objects[i] + "\nendobj\n"); }
                var xref = stream.Position; write("xref\n0 " + offsets.Count + "\n0000000000 65535 f \n");
                for (int i = 1; i < offsets.Count; i++) write(offsets[i].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
                write("trailer\n<< /Size " + offsets.Count + " /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n"); return stream.ToArray();
            }
        }
    }
}
