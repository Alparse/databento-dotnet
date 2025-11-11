using Databento.Client.Models;
using Databento.Client.Models.Dbn;

namespace Databento.Client.Dbn;

/// <summary>
/// Writer for DBN (Databento Binary) format files
/// </summary>
public interface IDbnFileWriter : IDisposable
{
    /// <summary>
    /// Write a single record to the DBN file
    /// </summary>
    /// <param name="record">Record to write</param>
    void WriteRecord(Record record);

    /// <summary>
    /// Write multiple records to the DBN file
    /// </summary>
    /// <param name="records">Records to write</param>
    void WriteRecords(IEnumerable<Record> records);

    /// <summary>
    /// Flush any buffered data to disk
    /// </summary>
    void Flush();
}
