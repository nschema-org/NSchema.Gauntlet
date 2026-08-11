namespace NSchema.Gauntlet.Services.Engines.Postgres;

/// <summary>
/// The catalog relations the oracle reads, and how each one is made comparable between two databases.
/// </summary>
/// <remarks>
/// <para>
/// Every query returns an identity and the whole catalog row as jsonb. Starting from <c>to_jsonb(t)</c>
/// means a query names only what it <em>removes</em> or <em>rewrites</em> — everything else is carried
/// through without being asked for. That is the point: the previous oracle compared a hand-written
/// projection, so an attribute nobody thought of was silently not compared, and partitioning, filtered
/// indexes and computed columns all hid in that gap. Here a new attribute is compared by default, and a
/// gap has to be written down to exist.
/// </para>
/// <para>
/// Three things do have to be written down, and they are the only hand-maintained part:
/// </para>
/// <list type="bullet">
///   <item><b>Dropped</b> — identity and physical storage that legitimately differs between two databases
///   holding the same schema: <c>oid</c>, <c>relfilenode</c>, <c>relfrozenxid</c>, and the planner's
///   statistics.</item>
///   <item><b>Resolved</b> — an OID that is a <em>reference</em>. Dropping it would lose the relationship
///   and quietly make two indexes on different tables identical, so it is cast to the name it points at
///   (<c>indrelid::regclass</c>) instead.</item>
///   <item><b>Reduced</b> — a reference to something the engine generated, whose name still carries an OID
///   in it (<c>reltoastrelid</c> resolves to <c>pg_toast_16386</c>). Only its presence can be compared.</item>
///   <item><b>Out of scope</b> — ownership, and whether a materialized view holds rows. NSchema models
///   neither, and the gauntlet is schema-only, so <c>relowner</c>, its siblings and <c>relispopulated</c>
///   are dropped rather than compared: holding a tool to something it never set out to manage produces a
///   red cell nobody can act on. Grants are a different matter and are still compared, because NSchema does
///   model those.</item>
/// </list>
/// <para>
/// Where a catalog stores a parse tree rather than text — <c>conbin</c>, <c>indexprs</c>, <c>adbin</c> —
/// the tree is full of OIDs and cannot be compared directly, so the raw column is dropped and the engine's
/// own deparser supplies the text instead.
/// </para>
/// </remarks>
public static class PostgresCatalogs
{
    private const string UserSchemas = "n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'";

    /// <summary>
    /// Each relation, and a query returning <c>(identity, row)</c> where the row is jsonb rendered as text.
    /// </summary>
    public static readonly (string Name, string Sql)[] All =
    [
        // public is excluded because NSchema excludes implicit schemas from its diff by design, so asserting
        // on one holds it to something it never set out to manage. Its ACL is the visible cost: a fresh
        // Postgres 15+ database owns public by pg_database_owner, a dump that says OWNER TO postgres changes
        // it, and the ACL is written relative to whoever that is. Grants on schemas anyone actually declared
        // are still compared.
        ("pg_namespace",
            $"""
             SELECT n.nspname,
                    (to_jsonb(n) - 'oid' - 'nspowner')::text
             FROM pg_namespace n
             WHERE {UserSchemas} AND n.nspname <> 'public'
             """),

        ("pg_class",
            $"""
             SELECT n.nspname || '.' || c.relname,
                    (to_jsonb(c)
                       - 'oid' - 'relfilenode' - 'relfrozenxid' - 'relminmxid'
                       - 'relpages' - 'reltuples' - 'relallvisible' - 'relowner' - 'relispopulated'
                       - 'relpartbound'
                     || jsonb_build_object(
                          'relnamespace', c.relnamespace::regnamespace::text,
                          'reltype', CASE WHEN c.reltype = 0 THEN NULL ELSE c.reltype::regtype::text END,
                          'reloftype', CASE WHEN c.reloftype = 0 THEN NULL ELSE c.reloftype::regtype::text END,
                          'relam', CASE WHEN c.relam = 0 THEN NULL ELSE (SELECT amname FROM pg_am WHERE oid = c.relam) END,
                          'reltoastrelid', c.reltoastrelid <> 0,
                          'relrewrite', c.relrewrite <> 0))::text
             -- relpartbound is dropped rather than deparsed here: it is a parse tree carrying type OIDs and
             -- raw datum bytes, and pg_inherits already reports the same bound as readable text.
             FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE {UserSchemas}
             """),

        ("pg_attribute",
            $"""
             SELECT n.nspname || '.' || c.relname || '.' || a.attname,
                    (to_jsonb(a)
                       - 'attrelid' - 'atttypid' - 'attcollation' - 'attmissingval'
                       -- attndims records the dimensions an array column was declared with, and Postgres neither
                       -- enforces them nor lets them mean anything: text[] and text[][] are the same type, and a
                       -- value of any dimensionality fits either. Nothing behaves differently, so nothing is lost.
                       - 'attndims'
                     || jsonb_build_object(
                          'attrelid', n.nspname || '.' || c.relname,
                          'atttypid', a.atttypid::regtype::text,
                          'attcollation', CASE WHEN a.attcollation = 0 THEN NULL
                                               ELSE (SELECT collname FROM pg_collation WHERE oid = a.attcollation) END))::text
             FROM pg_attribute a
             JOIN pg_class c ON c.oid = a.attrelid
             JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE a.attnum > 0 AND NOT a.attisdropped AND {UserSchemas}
             """),

        // adbin is a parse tree; the deparsed text is the comparable form.
        ("pg_attrdef",
            $"""
             SELECT n.nspname || '.' || c.relname || '.' || a.attname,
                    jsonb_build_object('default', pg_get_expr(d.adbin, d.adrelid))::text
             FROM pg_attrdef d
             JOIN pg_class c ON c.oid = d.adrelid
             JOIN pg_namespace n ON n.oid = c.relnamespace
             JOIN pg_attribute a ON a.attrelid = d.adrelid AND a.attnum = d.adnum
             WHERE {UserSchemas}
             """),

        ("pg_constraint",
            $"""
             SELECT n.nspname || '.' || COALESCE(cl.relname || '.', '') || con.conname,
                    (to_jsonb(con)
                       - 'oid' - 'connamespace' - 'conrelid' - 'contypid' - 'conindid' - 'conparentid'
                       - 'confrelid' - 'conbin' - 'conpfeqop' - 'conppeqop' - 'conffeqop' - 'conexclop'
                     || jsonb_build_object(
                          'connamespace', con.connamespace::regnamespace::text,
                          'conrelid', CASE WHEN con.conrelid = 0 THEN NULL ELSE con.conrelid::regclass::text END,
                          'contypid', CASE WHEN con.contypid = 0 THEN NULL ELSE con.contypid::regtype::text END,
                          'confrelid', CASE WHEN con.confrelid = 0 THEN NULL ELSE con.confrelid::regclass::text END,
                          'definition', pg_get_constraintdef(con.oid)))::text
             FROM pg_constraint con
             JOIN pg_namespace n ON n.oid = con.connamespace
             LEFT JOIN pg_class cl ON cl.oid = con.conrelid
             WHERE {UserSchemas}
             """),

        ("pg_index",
            $"""
             SELECT n.nspname || '.' || ic.relname,
                    (to_jsonb(i)
                       - 'indexrelid' - 'indrelid' - 'indcollation' - 'indclass' - 'indexprs' - 'indpred'
                     || jsonb_build_object(
                          'indexrelid', n.nspname || '.' || ic.relname,
                          'indrelid', i.indrelid::regclass::text,
                          'definition', pg_get_indexdef(i.indexrelid)))::text
             FROM pg_index i
             JOIN pg_class ic ON ic.oid = i.indexrelid
             JOIN pg_namespace n ON n.oid = ic.relnamespace
             WHERE {UserSchemas}
             """),

        ("pg_proc",
            $"""
             SELECT n.nspname || '.' || p.proname || '(' || pg_get_function_identity_arguments(p.oid) || ')',
                    (to_jsonb(p)
                       - 'oid' - 'pronamespace' - 'proowner' - 'prolang' - 'provariadic' - 'prosupport'
                       - 'prorettype' - 'proargtypes' - 'proallargtypes' - 'protrftypes' - 'proargdefaults'
                     || jsonb_build_object(
                          'pronamespace', p.pronamespace::regnamespace::text,
                          'prolang', (SELECT lanname FROM pg_language WHERE oid = p.prolang),
                          'prorettype', p.prorettype::regtype::text,
                          'proargtypes', (SELECT string_agg(t::regtype::text, ',' ORDER BY ord)
                                          FROM unnest(p.proargtypes) WITH ORDINALITY AS u(t, ord)),
                          'proallargtypes', (SELECT string_agg(t::regtype::text, ',' ORDER BY ord)
                                             FROM unnest(p.proallargtypes) WITH ORDINALITY AS u(t, ord)),
                          'proargdefaults', pg_get_expr(p.proargdefaults, 0)))::text
             FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
             WHERE {UserSchemas}
             """),

        ("pg_type",
            $"""
             SELECT n.nspname || '.' || t.typname,
                    (to_jsonb(t)
                       - 'oid' - 'typnamespace' - 'typowner' - 'typrelid' - 'typelem' - 'typarray'
                       - 'typbasetype' - 'typcollation' - 'typinput' - 'typoutput' - 'typreceive'
                       - 'typsend' - 'typmodin' - 'typmodout' - 'typanalyze' - 'typsubscript'
                       - 'typdefaultbin'
                     || jsonb_build_object(
                          'typnamespace', t.typnamespace::regnamespace::text,
                          'typrelid', CASE WHEN t.typrelid = 0 THEN NULL ELSE t.typrelid::regclass::text END,
                          'typelem', CASE WHEN t.typelem = 0 THEN NULL ELSE t.typelem::regtype::text END,
                          'typbasetype', CASE WHEN t.typbasetype = 0 THEN NULL ELSE t.typbasetype::regtype::text END,
                          'typcollation', CASE WHEN t.typcollation = 0 THEN NULL
                                               ELSE (SELECT collname FROM pg_collation WHERE oid = t.typcollation) END,
                          'typdefaultbin', pg_get_expr(t.typdefaultbin, 0)))::text
             FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
             WHERE {UserSchemas}
             """),

        ("pg_enum",
            $"""
             SELECT n.nspname || '.' || t.typname || '.' || e.enumlabel,
                    (to_jsonb(e) - 'oid' - 'enumtypid'
                     || jsonb_build_object('enumtypid', e.enumtypid::regtype::text))::text
             FROM pg_enum e
             JOIN pg_type t ON t.oid = e.enumtypid
             JOIN pg_namespace n ON n.oid = t.typnamespace
             WHERE {UserSchemas}
             """),

        ("pg_trigger",
            $"""
             SELECT n.nspname || '.' || c.relname || '.' || tg.tgname,
                    (to_jsonb(tg)
                       - 'oid' - 'tgrelid' - 'tgfoid' - 'tgconstrrelid' - 'tgconstrindid' - 'tgconstraint'
                       - 'tgqual' - 'tgargs' - 'tgparentid'
                     || jsonb_build_object(
                          'tgrelid', tg.tgrelid::regclass::text,
                          'definition', pg_get_triggerdef(tg.oid)))::text
             FROM pg_trigger tg
             JOIN pg_class c ON c.oid = tg.tgrelid
             JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE NOT tg.tgisinternal AND {UserSchemas}
             """),

        ("pg_sequence",
            $"""
             SELECT n.nspname || '.' || c.relname,
                    (to_jsonb(s) - 'seqrelid' - 'seqtypid'
                     || jsonb_build_object('seqtypid', s.seqtypid::regtype::text))::text
             FROM pg_sequence s
             JOIN pg_class c ON c.oid = s.seqrelid
             JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE {UserSchemas}
             """),

        // Partitioning and classic inheritance: which parent, in what order, and for what values.
        ("pg_inherits",
            $"""
             SELECT n.nspname || '.' || c.relname,
                    jsonb_build_object(
                      'parent', i.inhparent::regclass::text,
                      'seqno', i.inhseqno,
                      'bound', pg_get_expr(c.relpartbound, c.oid))::text
             FROM pg_inherits i
             JOIN pg_class c ON c.oid = i.inhrelid
             JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE {UserSchemas}
             """),

        ("pg_partitioned_table",
            $"""
             SELECT n.nspname || '.' || c.relname,
                    (to_jsonb(pt)
                       - 'partrelid' - 'partdefid' - 'partclass' - 'partcollation' - 'partexprs'
                     || jsonb_build_object('key', pg_get_partkeydef(pt.partrelid)))::text
             FROM pg_partitioned_table pt
             JOIN pg_class c ON c.oid = pt.partrelid
             JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE {UserSchemas}
             """),

        // A view's rule is a parse tree; its deparsed body is the comparable form.
        ("view_definition",
            $"""
             SELECT n.nspname || '.' || c.relname,
                    jsonb_build_object('definition', pg_get_viewdef(c.oid, true))::text
             FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
             WHERE c.relkind IN ('v', 'm') AND {UserSchemas}
             """),

        ("pg_description",
            $"""
             SELECT COALESCE(n.nspname || '.' || c.relname, d.objoid::text)
                    || CASE WHEN d.objsubid > 0 THEN '.' || a.attname ELSE '' END,
                    jsonb_build_object('description', d.description)::text
             FROM pg_description d
             JOIN pg_class c ON c.oid = d.objoid
             JOIN pg_namespace n ON n.oid = c.relnamespace
             LEFT JOIN pg_attribute a ON a.attrelid = d.objoid AND a.attnum = d.objsubid
             WHERE {UserSchemas}
             """),
    ];
}
