namespace NSchema.Gauntlet.Services.Engines.Sqlite;

/// <summary>
/// The relations the Sqlite oracle reads, and how each one is made comparable between two databases.
/// </summary>
/// <remarks>
/// <para>
/// Sqlite has no system catalogs to dump the way Postgres and SQL Server do: <c>sqlite_master</c> holds the DDL
/// text an author wrote, and everything the engine actually decided is behind a pragma. So the same principle
/// arrives differently — each pragma is selected whole and every column it returns is compared, with the reader
/// enumerating them rather than the query naming them. A pragma that gains a column in a later Sqlite is compared
/// from then on without anyone editing this file, which is the property that matters: the previous oracle listed
/// the columns it wanted, and what it did not think to ask for could not fail.
/// </para>
/// <para>
/// The pragmas are the whole of the schema Sqlite will admit to. <c>table_list</c> carries <c>WITHOUT ROWID</c>
/// and <c>STRICT</c>; <c>table_xinfo</c> carries generated columns, which <c>table_info</c> hides;
/// <c>index_list</c> says whether an index is partial and whether Sqlite made it itself; <c>index_xinfo</c> covers
/// every key and auxiliary column, including the expression ones a column name cannot describe; and
/// <c>foreign_key_list</c> was missing from the oracle altogether.
/// </para>
/// <para>
/// What no pragma reports is the <em>text</em> of a check constraint, of a partial index's <c>WHERE</c>, or of an
/// expression index's expression. Those live only in <c>sqlite_master.sql</c>, and that column is deliberately not
/// compared: it is the statement an author wrote against the statement NSchema writes, so it differs wherever the
/// two spell the same schema differently — quoting, layout, the case of a type name — and every difference it
/// reported on Chinook was one the pragmas had already reported at column level, or one Sqlite does not recognise
/// as a difference at all. Making it agree would mean lowercasing the whole statement, which equally hides
/// <c>DEFAULT 'Hello'</c> becoming <c>DEFAULT 'hello'</c>. So the remaining blind spot is narrow and named: the
/// predicate text of a check or a partial index, and the expression of an expression index. Their existence,
/// though, is visible — <c>index_list.partial</c> and an <c>index_xinfo</c> key column with no name both show up.
/// </para>
/// </remarks>
public static class SqliteCatalogs
{
    /// <summary>
    /// The user objects, ignoring the ones Sqlite maintains for itself.
    /// </summary>
    private const string UserObjects = "m.name NOT LIKE 'sqlite_%'";

    /// <summary>
    /// Every relation the oracle reads.
    /// </summary>
    public static readonly IReadOnlyList<SqliteCatalog> All =
    [
        // Whether a table is WITHOUT ROWID or STRICT lives nowhere else, and both change what the table is.
        new("table_list",
            """
            SELECT l.* FROM pragma_table_list l
            WHERE l.schema = 'main' AND l.name NOT LIKE 'sqlite_%'
            """,
            ["name"],
            "schema"),

        // xinfo rather than info: a generated column is hidden from the latter, so a schema could lose one
        // without the oracle noticing.
        //
        // The declared type is compared in one case. Sqlite does not care how a type is spelled — it reads the
        // affinity rules and moves on — so NVARCHAR and nvarchar are the same column, and the previous oracle went
        // further and compared only the affinity. This keeps the word, so VARCHAR against NVARCHAR is still a
        // difference, and drops only the case.
        //
        // NUMERIC and DECIMAL go the same way, as they do on SQL Server: everything Sqlite does not recognise as
        // text, blob, real or integer takes numeric affinity, so the two words name one type here even more
        // plainly than they do there.
        new("table_xinfo",
            $"""
             SELECT m.name AS "table", x.*, replace(upper(x.type), 'NUMERIC', 'DECIMAL') AS "type__as"
             FROM sqlite_master m JOIN pragma_table_xinfo(m.name) x
             WHERE m.type = 'table' AND {UserObjects}
             """,
            ["table", "name"]),

        new("index_list",
            $"""
             SELECT m.name AS "table", i.* FROM sqlite_master m JOIN pragma_index_list(m.name) i
             WHERE m.type = 'table' AND {UserObjects}
             """,
            ["table", "name"],
            "seq"),

        // Keyed by position rather than by column, because an expression index has no column name to key on and
        // two of them would otherwise collapse onto one identity.
        new("index_xinfo",
            $"""
             SELECT i.name AS "index", x.* FROM sqlite_master m
             JOIN pragma_index_list(m.name) i
             JOIN pragma_index_xinfo(i.name) x
             WHERE m.type = 'table' AND {UserObjects}
             """,
            ["index", "seqno"]),

        // Absent from the previous oracle entirely: a rebuild could have dropped every foreign key and passed.
        new("foreign_key_list",
            $"""
             SELECT m.name AS "table", f.* FROM sqlite_master m JOIN pragma_foreign_key_list(m.name) f
             WHERE m.type = 'table' AND {UserObjects}
             """,
            ["table", "id", "seq"]),

        // What exists and what kind of thing it is. rootpage is where the object physically starts, which two
        // databases holding the same schema have no reason to agree on, and the DDL text is left out for the
        // reason given on the type above.
        new("sqlite_master",
            $"""
             SELECT m.name, m.type, m.tbl_name FROM sqlite_master m
             WHERE {UserObjects}
             """,
            ["name"],
            "rootpage"),
    ];
}
