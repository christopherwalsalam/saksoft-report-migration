namespace ReportConversion.Domain.Entities;

public class ReportSqlAnalysis
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public string? TablesReferenced { get; set; }   // comma-separated table names extracted from SQL
    public string? ColumnsUsed { get; set; }         // key columns from SELECT / WHERE / GROUP BY
    public string? AggregationPatterns { get; set; } // detected SUM, COUNT, AVG, MAX, MIN, GROUP BY
    public string? JoinPatterns { get; set; }        // join types and joined tables
    public string? FilterPatterns { get; set; }      // WHERE clause keyword patterns
    public string? SqlSummary { get; set; }          // compact summary sent to LLM for grouping
    public DateTime AnalysedAt { get; set; }
    public Report? Report { get; set; }
}
