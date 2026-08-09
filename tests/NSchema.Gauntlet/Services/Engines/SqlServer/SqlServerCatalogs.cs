namespace NSchema.Gauntlet.Services.Engines.SqlServer;

/// <summary>
/// The catalog relations the oracle reads, and how each one is made comparable between two databases.
/// </summary>
/// <remarks>
/// <para>
/// Every query selects <c>*</c> and lets <c>FOR JSON</c> serialise whatever the row holds, so an attribute
/// nobody thought about is compared by default. That is the whole point: the previous oracle compared a
/// hand-written projection, and filtered indexes, included columns, computed columns and column defaults
/// all sat in the gap between what it projected and what SQL Server actually records.
/// </para>
/// <para>
/// <c>INCLUDE_NULL_VALUES</c> is not optional. Without it SQL Server omits null attributes entirely, so a
/// value going from something to nothing would read as the attribute never having existed.
/// </para>
/// <para>
/// T-SQL has no way to subtract a key from a JSON object, so unlike the Postgres side the exclusions live
/// in <see cref="SqlServerCatalog.Exclude"/> rather than in the query. The categories are the same:
/// identity (<c>object_id</c>, <c>schema_id</c>) and physical or temporal facts (<c>create_date</c>,
/// <c>modify_date</c>, <c>last_value</c>) are dropped; ids that are really <em>references</em> are resolved
/// to the name they point at, because dropping them would quietly make two indexes on different tables
/// identical. Ownership is dropped throughout: NSchema does not model it.
/// </para>
/// </remarks>
public static class SqlServerCatalogs
{
    private const string Json = "FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES";

    // decimal and numeric are the same type under two spellings — SQL Server documents them as synonyms and
    // gives them separate type ids anyway. A script that says NUMERIC(10,2) and a rebuild that says
    // DECIMAL(10,2) hold the same column, so the name is normalised rather than reported as a difference.
    private static string TypeName(string alias) =>
        $"CASE {alias}.name WHEN 'numeric' THEN 'decimal' ELSE {alias}.name END";

    // A constraint nobody named gets one from SQL Server ending in a hash of the object id, so it differs
    // between two databases and between two runs against the same script. Primary and unique keys take
    // sixteen hex digits, defaults and checks eight, and the longer case has to be tried first or it is
    // mistaken for the shorter one with hex left over. The hash is masked rather than the name dropped: the
    // prefix still says which table and column it belongs to, and whether a name was generated at all is
    // exactly what the naming findings turn on.
    private static string MaskedName(string alias) =>
        $"""
         CASE WHEN {alias}.name LIKE '%[_][_]{Hex(16)}' THEN LEFT({alias}.name, LEN({alias}.name) - 16) + '(auto)'
              WHEN {alias}.name LIKE '%[_][_]{Hex(8)}' THEN LEFT({alias}.name, LEN({alias}.name) - 8) + '(auto)'
              ELSE {alias}.name END
         """;

    private static string Hex(int digits) => string.Concat(Enumerable.Repeat("[0-9A-F]", digits));

    // The fixed database roles each own a schema, and none of them is anybody's schema.
    private const string UserSchemas =
        """
        s.name NOT IN ('sys', 'INFORMATION_SCHEMA', 'guest', 'db_owner', 'db_accessadmin',
                       'db_securityadmin', 'db_ddladmin', 'db_backupoperator', 'db_datareader',
                       'db_datawriter', 'db_denydatareader', 'db_denydatawriter')
        """;

    /// <summary>Identity and physical facts carried by nearly every <c>sys.objects</c>-shaped relation.</summary>
    private static readonly string[] ObjectKeys =
        ["object_id", "schema_id", "principal_id", "parent_object_id", "create_date", "modify_date"];

    /// <summary>
    /// Replication and publication bookkeeping, dropped from every relation that carries it.
    /// </summary>
    /// <remarks>
    /// These are false for every user object — the queries already filter on <c>is_ms_shipped = 0</c> — so
    /// they never carry information. They are worth excluding rather than tolerating because they ride along
    /// on <em>every</em> relation: when an object's identity differs between the two databases, each one
    /// turns into a pair of findings saying nothing.
    /// </remarks>
    public static readonly IReadOnlySet<string> Bookkeeping =
        new HashSet<string>(StringComparer.Ordinal) { "is_ms_shipped", "is_published", "is_schema_published" };

    /// <summary>
    /// Every relation the oracle reads.
    /// </summary>
    public static readonly SqlServerCatalog[] All =
    [
        // dbo is left out for the same reason public is on the Postgres side: NSchema excludes implicit
        // schemas from its diff, so asserting on one holds it to something it never set out to manage.
        new("sys.schemas",
            $"""
             SELECT s.name, (SELECT s.* {Json}), NULL
             FROM sys.schemas s
             WHERE {UserSchemas} AND s.name <> 'dbo'
             """,
            "schema_id", "principal_id"),

        new("sys.tables",
            $"""
             SELECT s.name + '.' + t.name, (SELECT t.* {Json}),
                    (SELECT s.name AS [schema_id] {Json})
             FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
             WHERE t.is_ms_shipped = 0 AND {UserSchemas}
             """,
            [.. ObjectKeys, "lob_data_space_id", "filestream_data_space_id", "history_table_id"]),

        new("sys.views",
            $"""
             SELECT s.name + '.' + v.name, (SELECT v.* {Json}),
                    (SELECT s.name AS [schema_id] {Json})
             FROM sys.views v JOIN sys.schemas s ON s.schema_id = v.schema_id
             WHERE v.is_ms_shipped = 0 AND {UserSchemas}
             """,
            ObjectKeys),

        new("sys.columns",
            $"""
             SELECT s.name + '.' + o.name + '.' + c.name, (SELECT c.* {Json}),
                    (SELECT {TypeName("ty")} AS [user_type_id], {TypeName("sty")} AS [system_type_id],
                            xsc.name AS [xml_collection_id] {Json})
             FROM sys.columns c
             JOIN sys.objects o ON o.object_id = c.object_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             JOIN sys.types ty ON ty.user_type_id = c.user_type_id
             LEFT JOIN sys.types sty ON sty.user_type_id = c.system_type_id
             LEFT JOIN sys.xml_schema_collections xsc ON xsc.xml_collection_id = c.xml_collection_id
             WHERE o.is_ms_shipped = 0 AND {UserSchemas}
             """,
            "object_id", "default_object_id", "rule_object_id", "collation_id"),

        // The computed-column expression, which the old oracle could not see at all.
        new("sys.computed_columns",
            $"""
             SELECT s.name + '.' + o.name + '.' + c.name, (SELECT c.* {Json}), NULL
             FROM sys.computed_columns c
             JOIN sys.objects o ON o.object_id = c.object_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             WHERE o.is_ms_shipped = 0 AND {UserSchemas}
             """,
            "object_id", "default_object_id", "rule_object_id", "collation_id",
            "system_type_id", "user_type_id", "xml_collection_id"),

        new("sys.identity_columns",
            $"""
             SELECT s.name + '.' + o.name + '.' + c.name, (SELECT c.* {Json}), NULL
             FROM sys.identity_columns c
             JOIN sys.objects o ON o.object_id = c.object_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             WHERE o.is_ms_shipped = 0 AND {UserSchemas}
             """,
            "object_id", "default_object_id", "rule_object_id", "collation_id",
            "system_type_id", "user_type_id", "xml_collection_id", "last_value"),

        // Keyed by the column it defaults rather than by its own name. A default belongs to exactly one
        // column, and its name is frequently auto-generated — so naming it by name means that when the name
        // differs, every attribute of the row reads as one loss and one unrelated gain, and a single naming
        // bug arrives as a dozen findings. Keyed by column, the name is just another attribute that changed.
        new("sys.default_constraints",
            $"""
             SELECT s.name + '.' + t.name + '.' + COALESCE(c.name, dc.name), (SELECT dc.* {Json}),
                    (SELECT c.name AS [parent_column_id], {MaskedName("dc")} AS [name] {Json})
             FROM sys.default_constraints dc
             JOIN sys.objects t ON t.object_id = dc.parent_object_id
             JOIN sys.schemas s ON s.schema_id = t.schema_id
             LEFT JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
             WHERE t.is_ms_shipped = 0 AND {UserSchemas}
             """,
            ObjectKeys),

        new("sys.check_constraints",
            $"""
             SELECT s.name + '.' + t.name + '.' + {MaskedName("cc")}, (SELECT cc.* {Json}),
                    (SELECT c.name AS [parent_column_id], {MaskedName("cc")} AS [name] {Json})
             FROM sys.check_constraints cc
             JOIN sys.objects t ON t.object_id = cc.parent_object_id
             JOIN sys.schemas s ON s.schema_id = t.schema_id
             LEFT JOIN sys.columns c ON c.object_id = cc.parent_object_id AND c.column_id = cc.parent_column_id
             WHERE t.is_ms_shipped = 0 AND {UserSchemas}
             """,
            ObjectKeys),

        new("sys.foreign_keys",
            $"""
             SELECT s.name + '.' + t.name + '.' + {MaskedName("fk")}, (SELECT fk.* {Json}),
                    (SELECT rs.name + '.' + rt.name AS [referenced_object_id],
                            {MaskedName("fk")} AS [name] {Json})
             FROM sys.foreign_keys fk
             JOIN sys.objects t ON t.object_id = fk.parent_object_id
             JOIN sys.schemas s ON s.schema_id = t.schema_id
             JOIN sys.objects rt ON rt.object_id = fk.referenced_object_id
             JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
             WHERE t.is_ms_shipped = 0 AND {UserSchemas}
             """,
            [.. ObjectKeys, "key_index_id"]),

        new("sys.foreign_key_columns",
            $"""
             SELECT s.name + '.' + t.name + '.' + {MaskedName("fk")} + '.' + CAST(fkc.constraint_column_id AS varchar(10)),
                    (SELECT fkc.* {Json}),
                    (SELECT pc.name AS [parent_column_id], rc.name AS [referenced_column_id],
                            rs.name + '.' + rt.name AS [referenced_object_id] {Json})
             FROM sys.foreign_key_columns fkc
             JOIN sys.foreign_keys fk ON fk.object_id = fkc.constraint_object_id
             JOIN sys.objects t ON t.object_id = fkc.parent_object_id
             JOIN sys.schemas s ON s.schema_id = t.schema_id
             JOIN sys.objects rt ON rt.object_id = fkc.referenced_object_id
             JOIN sys.schemas rs ON rs.schema_id = rt.schema_id
             LEFT JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
             LEFT JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
             WHERE t.is_ms_shipped = 0 AND {UserSchemas}
             """,
            "constraint_object_id", "parent_object_id"),

        new("sys.key_constraints",
            $"""
             SELECT s.name + '.' + t.name + '.' + {MaskedName("kc")}, (SELECT kc.* {Json}),
                    (SELECT {MaskedName("kc")} AS [name] {Json})
             FROM sys.key_constraints kc
             JOIN sys.objects t ON t.object_id = kc.parent_object_id
             JOIN sys.schemas s ON s.schema_id = t.schema_id
             WHERE t.is_ms_shipped = 0 AND {UserSchemas}
             """,
            // is_system_named is excluded: where the source let SQL Server name a constraint, NSchema reproduces
            // that name explicitly, so the flag flips while the name matches. Naming something the engine would
            // otherwise invent is the opposite of a fidelity loss — the same judgement that says a generated name
            // is not worth carrying into a project file.
            [.. ObjectKeys, "unique_index_id", "is_system_named"]),

        // filter_definition lives here: a filtered index and an unfiltered one were previously identical.
        new("sys.indexes",
            $"""
             SELECT s.name + '.' + o.name + '.' + COALESCE(i.name, '(heap)'), (SELECT i.* {Json}),
                    (SELECT ds.name + ' (' + ds.type_desc + ')' AS [data_space_id] {Json})
             FROM sys.indexes i
             JOIN sys.objects o ON o.object_id = i.object_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             LEFT JOIN sys.data_spaces ds ON ds.data_space_id = i.data_space_id
             WHERE o.is_ms_shipped = 0 AND o.type IN ('U', 'V') AND {UserSchemas}
             """,
            "object_id", "index_id"),

        // is_included_column lives here: an INCLUDE and a bare index were previously identical.
        new("sys.index_columns",
            $"""
             SELECT s.name + '.' + o.name + '.' + COALESCE(i.name, '(heap)') + '.' + c.name,
                    (SELECT ic.* {Json}), NULL
             FROM sys.index_columns ic
             JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
             JOIN sys.objects o ON o.object_id = ic.object_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
             WHERE o.is_ms_shipped = 0 AND o.type IN ('U', 'V') AND {UserSchemas}
             """,
            "object_id", "index_id", "column_id"),

        new("sys.objects",
            $"""
             SELECT s.name + '.' + o.name, (SELECT o.* {Json}),
                    (SELECT s.name AS [schema_id] {Json})
             FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id
             WHERE o.is_ms_shipped = 0 AND o.type IN ('FN', 'IF', 'TF', 'P', 'AF', 'FS', 'FT')
               AND {UserSchemas}
             """,
            ObjectKeys),

        new("sys.sql_modules",
            $"""
             SELECT s.name + '.' + o.name, (SELECT m.* {Json}), NULL
             FROM sys.sql_modules m
             JOIN sys.objects o ON o.object_id = m.object_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             WHERE o.is_ms_shipped = 0 AND {UserSchemas}
             """,
            "object_id"),

        new("sys.triggers",
            $"""
             SELECT s.name + '.' + t.name + '.' + tr.name, (SELECT tr.* {Json}), NULL
             FROM sys.triggers tr
             JOIN sys.objects t ON t.object_id = tr.parent_id
             JOIN sys.schemas s ON s.schema_id = t.schema_id
             WHERE tr.is_ms_shipped = 0 AND {UserSchemas}
             """,
            "object_id", "parent_id", "create_date", "modify_date"),

        new("sys.sequences",
            $"""
             SELECT s.name + '.' + sq.name, (SELECT sq.* {Json}),
                    (SELECT {TypeName("ty")} AS [user_type_id], {TypeName("sty")} AS [system_type_id] {Json})
             FROM sys.sequences sq
             JOIN sys.schemas s ON s.schema_id = sq.schema_id
             LEFT JOIN sys.types ty ON ty.user_type_id = sq.user_type_id
             LEFT JOIN sys.types sty ON sty.user_type_id = sq.system_type_id
             WHERE sq.is_ms_shipped = 0 AND {UserSchemas}
             """,
            [.. ObjectKeys, "current_value", "last_used_value"]),

        new("sys.types",
            $"""
             SELECT s.name + '.' + ty.name, (SELECT ty.* {Json}),
                    (SELECT {TypeName("sty")} AS [system_type_id] {Json})
             FROM sys.types ty
             JOIN sys.schemas s ON s.schema_id = ty.schema_id
             LEFT JOIN sys.types sty ON sty.user_type_id = ty.system_type_id
             WHERE ty.is_user_defined = 1 AND {UserSchemas}
             """,
            "user_type_id", "schema_id", "principal_id", "default_object_id", "rule_object_id", "collation_id"),

        new("sys.xml_schema_collections",
            $"""
             SELECT s.name + '.' + x.name, (SELECT x.* {Json}), NULL
             FROM sys.xml_schema_collections x
             JOIN sys.schemas s ON s.schema_id = x.schema_id
             WHERE {UserSchemas} AND x.xml_collection_id > 65535
             """,
            "xml_collection_id", "schema_id", "principal_id", "create_date", "modify_date"),

        // The value is trimmed rather than compared verbatim. NSchema carries a description through NSQL as a doc
        // comment, and the lexer trims those — deliberately, because the writer and the formatter would fight over
        // trailing whitespace otherwise, and the corpus asserts that formatting is a no-op. So a description that
        // arrives with trailing spaces cannot survive a round trip, and should not: it is normalisation, not loss.
        new("sys.extended_properties",
            $"""
             SELECT s.name + '.' + o.name + COALESCE('.' + c.name, '') + ' :: ' + ep.name,
                    (SELECT LTRIM(RTRIM(CONVERT(nvarchar(max), ep.value))) AS [value] {Json}), NULL
             FROM sys.extended_properties ep
             JOIN sys.objects o ON o.object_id = ep.major_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             LEFT JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id AND ep.minor_id > 0
             WHERE ep.class = 1 AND o.is_ms_shipped = 0 AND {UserSchemas}
             """),

        new("sys.partition_functions",
            $"SELECT pf.name, (SELECT pf.* {Json}), NULL FROM sys.partition_functions pf",
            "function_id", "create_date", "modify_date"),

        new("sys.partition_range_values",
            $"""
             SELECT pf.name + '[' + CAST(rv.boundary_id AS varchar(10)) + ']',
                    (SELECT CONVERT(nvarchar(200), rv.value) AS [value], rv.parameter_id {Json}), NULL
             FROM sys.partition_range_values rv
             JOIN sys.partition_functions pf ON pf.function_id = rv.function_id
             """),

        new("sys.partition_schemes",
            $"""
             SELECT ps.name, (SELECT ps.* {Json}),
                    (SELECT pf.name AS [function_id] {Json})
             FROM sys.partition_schemes ps
             JOIN sys.partition_functions pf ON pf.function_id = ps.function_id
             """,
            "data_space_id"),

        // Which column a partitioned table is actually divided on. Everything above can survive intact
        // while nothing uses it, so this is the row that tells a partitioned table from a plain one.
        new("partition_column",
            $"""
             SELECT s.name + '.' + o.name + '.' + COALESCE(i.name, '(heap)'),
                    (SELECT c.name AS [column], ic.partition_ordinal {Json}), NULL
             FROM sys.index_columns ic
             JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
             JOIN sys.objects o ON o.object_id = ic.object_id
             JOIN sys.schemas s ON s.schema_id = o.schema_id
             JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
             WHERE ic.partition_ordinal > 0 AND o.is_ms_shipped = 0 AND {UserSchemas}
             """),
    ];
}
