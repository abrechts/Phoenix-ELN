# Phoenix ELN — Long-Term Archive Format: Design

Status: design note, not yet implemented. Covers the archival format, the
organisation-level aggregation model, and the integrity strategy.

The governing constraint: nothing on the share is mutable. Every consequence
below follows from it — no locking protocol, no verified backups, no recovery
apparatus, because there is no shared mutable state to protect. Section 8
records why a shared SQLite file written over SMB is not viable.

## 1. Problem

Phoenix ELN has four ways data leaves the application today:

| Mechanism | Purpose | Limitation |
|---|---|---|
| PDF/A-3b export | Human-readable record, print, audit | Terminal and lossy — nothing can read a yield or a reaction back out of it |
| `TransferPackage` | Move a whole installation to another machine | All-or-nothing, Phoenix→Phoenix, opaque to anything else |
| Server sync (MySQL) | Backup, sharing, org-wide search | Live working store; presumes a running server and a compatible schema |
| `.exp` JSON export/import | Move or copy one experiment between Phoenix installations | Serialized EF entity graph, not a documented interchange schema; version-coupled (import only guaranteed when exporter ≤ importer version) |

Only the `.exp` file is structured data, and it is Phoenix's own entity graph —
undocumented, coupled to the EF model, and of little use to a third party
without the source. None of the four is a documented interchange format, and
none is a preservation artefact that survives the application. That gap is what
this design closes.

Three separate deliverables are conflated under "export". They are listed here
in descending order of practical value and ascending order of effort.

### 1.1 Multi-experiment dataset export

A table across a *selection* of experiments: one row per experiment with
reaction SMILES, reference reactant, product, scale, equivalents of key
reagents, solvent, yield, purity, date, user, project.

Purposes: optimisation campaigns pasted into Excel/Origin, publication
supporting information, group reporting, reaction datasets for analysis or ML.

The natural hook is the **RSS hit list and the Same-Step list** — those already
assemble exactly the right rows, so "export these hits as CSV/XLSX" is a small
feature on top of existing query results. Highest value per unit of work of
anything in this document.

### 1.2 Per-experiment structured export

Same granularity as the PDF, but data rather than layout.

Recommended interchange format: **MDL RDfile**. An RDfile record is literally a
reaction plus arbitrary named data fields, which is exactly what a Phoenix
experiment is. `MDLRxnFileString` is already persisted, so generating one is
mostly string assembly with no new cheminformatics work, and the result is
readable by Reaxys, Pipeline Pilot, Chemaxon, Indigo and RDKit. A JSON variant
alongside preserves what the RDfile flattens, chiefly protocol ordering.

That JSON variant is the *same* documented schema as the section 2 payload, not
a second one, and it is distinct from the existing `.exp` format: `.exp` is
Phoenix's internal entity graph, serialized for re-import, and stays that way.
The payload instead carries the key experiment data a third party or an index
would query. Embedded files appear in neither its content nor its metadata —
they stay in the PDF (see section 2).

### 1.3 Programmatic API

Largely already satisfied: the local store is SQLite, so anyone with the file
and a documented schema has a complete read API through any language's sqlite3
binding. **Publishing the schema costs nothing and answers the data-lock-in
question outright.**

A REST API only makes sense on the optional server package, and only for group
dashboards, LIMS integration or automated reporting — the institutional
scenario. Not worth building speculatively.

### 1.4 Archival is not migration

The three deliverables above are interchange and migration features. The archive
described from section 2 onward is a different thing, and the two are kept
apart deliberately.

The archive preserves finalized work in a readable form and makes it findable:
open the document, read the data, search across years, verify the bytes. It does
**not** promise that an experiment can be rebuilt as live, editable state in
some future system. That is migration, and it is a different problem with
different preconditions — it is performed while Phoenix still runs, against the
local or server database, where the complete current model is available and no
format negotiation is needed. Three mechanisms already serve it: the `.exp`
export, `TransferPackage`, and direct SQLite access against a published schema.

The two also operate on different timescales, which is the real reason to
separate them. Migration happens once, soon, under the supervision of a working
installation. Archival has to keep working for decades with no installation at
all. A format that must satisfy both ends up constrained by the harder deadline
while carrying data the long-term reader never queries.

Migration from live data remains worth building. It is simply not this document.

## 2. The hybrid PDF/A-3b document

PDF/A-3b permits *arbitrary* embedded file attachments, not only PDFs — this is
the basis of hybrid invoice formats such as ZUGFeRD/Factur-X, where one file is
simultaneously a human-readable document and a machine-readable record. Phoenix
already uses A-3b attachments for inserted documents.

Therefore: **embed the structured payload inside the exported PDF**, with the
appropriate `AFRelationship` (`Data` or `Source`) and MIME type.

One artefact, both audiences, valid PDF/A-3b, reusing plumbing that already
exists. It also materially strengthens the archival claim: the document carries
its own structured data, so an experiment stays both readable *and* computable
with no Phoenix installation, no server and no index.

The payload therefore exists in three places, each for a different reader, all
generated from one serialisation in one operation:

| Location | Reader | Why there |
|---|---|---|
| Embedded in the PDF | Someone holding only the document | Self-sufficiency once the file is separated from the archive — emailed to a collaborator, sent to a patent attorney, attached as journal SI |
| Plain JSON sidecar beside the PDF | Index builders, third-party tools, a human with a text editor | Needs no PDF knowledge whatsoever; see section 3.5 |
| Columns in a generated volume | Query | Indexed search; disposable, see section 3.6 |

The duplication is deliberate and cheap — kilobytes against a document of
hundreds of kilobytes to hundreds of megabytes. Archival practice inverts the
database instinct: **normalise for databases, denormalise for archives.** Store
one `PayloadSha256` so divergence is detectable rather than silent.

That size ratio holds only because the payload excludes embedded files
altogether — not their bytes and not their metadata. Base64-inlining them, as
`.exp` does, would roughly double the artefact and store the same bytes twice,
since the files are already A-3b attachments in the same PDF. Attachment
metadata is left out too: it has no query value, and integrity is already
covered by `PdfSha256` over the whole document, which changes if any attached
byte does.

## 3. The archive: two tiers

### 3.1 Role

A **separate artefact** from the live `ELNData.db` and from `TransferPackage`,
with a different lifecycle: immutable, append-only, schema-stable, and
independent of the application version. The live database is mutable working
state tied to schema version N; the archive is the preservation record.

It has two tiers, and the distinction between them is the governing idea of this
design:

- **Record files — canonical.** One finalized experiment, written once, never
  reopened. These *are* the archive.
- **Volumes — derived.** Generated SQLite databases over the record files,
  providing indexed search. Regenerable at any time from tier 1, and therefore
  disposable.

### 3.2 Why the share holds files, not a database

The published consensus on SQLite over a network filesystem is "at your own
risk", and the specific hazards are worth naming because they determine the
design:

1. **Concurrent writers and advisory locking.** SMB/NFS lock semantics are
   unreliable, and this is the dominant corruption mechanism.
2. **Durability lies on interrupted writes.** Client-side caching and oplocks
   can acknowledge a write before it is durable, so a dropped connection
   mid-transaction can tear a file even with one writer.
3. **WAL cannot work across a share** at all, as it needs shared memory.

Note what is absent from that list: **copying a complete file to a share, and
writing a file once and never reopening it.** Both are plain sequential I/O that
SMB performs reliably, and both are what every backup tool already does. Hence
the rule from which everything else follows:

> The share only ever receives complete files, written once. It never hosts a
> mutable database.

A second, independent argument points the same way. SQLite's own benchmarking
("35% Faster Than The Filesystem") holds for *small* blobs — the documented
crossover is around 100 KB, above which direct file I/O wins. A Phoenix PDF with
embedded spectra can reach a few hundred megabytes, which is three orders of
magnitude past that. Blobs of that size in a shared container also cost:

- **File size.** Tens of thousands of experiments at even 10 MB average is
  hundreds of gigabytes in one file, which makes every maintenance operation
  expensive.
- **Backup.** "A single file is easy to back up" reverses at scale: a mutable
  container must be copied *in full, every time* — no file-granularity
  incremental, no deduplication, no write-once media. Write-once files are
  incrementally backed up by every tool in existence and never re-copied.
- **Blast radius.** Damage to a container is a container problem. Damage to a
  record file costs one record.

### 3.3 Why the query surface is still SQLite

An archive that cannot be searched efficiently is a write-only graveyard, so
indexed search is a first-class requirement, not an afterthought. **No query
ever parses a PDF.** Queries run against a volume — the same schema, the same
FTS5, the same query plans as the earlier shared-database draft. The only
differences are that the volume is derived rather than authoritative, and that
it holds a path rather than a blob.

A PDF is read in exactly one situation: the user opens a document. That is one
file, on demand, and it is cheaper as a file read than as a
multi-hundred-megabyte blob fetched through a database connection over SMB.

Keeping the index derived has a consequence that *strengthens* searchability
rather than weakening it. An authoritative index built in 2026 with FTS5 is the
index one is stuck with forever. A derived index is rebuilt in 2040 with whatever
exists then — including the Indigo reaction fingerprints section 3.4 excludes
from the archive, which a future reader regenerates from the archived rxnfiles
and then searches substructurally. Derived data belongs on the consuming side,
where it can be replaced.

### 3.4 Canonical versus derived data

The governing rule: **store the primitives from which indices can be rebuilt,
and treat indices as disposable.**

Canonical — self-describing, standards-based, multiple independent
implementations exist:

- the PDF/A-3b document
- `MDLRxnFileString` (CTfile specification is public and widely implemented)
- reaction SMILES
- `ReactantInChIKey` / `ProductInChIKey` (IUPAC standard, open reference
  implementation)
- `ExperimentJson`, `ProtocolText`

Derived, and regenerable from the above: the volumes, their FTS5 indices, and
any fingerprint index.

Deliberately **excluded**: Indigo reaction fingerprints and serialised
`RxnIndigoObj`. Both are opaque and locked to one library build — a future
reader would have to know the Indigo version, the fingerprint type, the bit
layout, and reimplement the screen-then-confirm logic. They are caches, not
records. A server-less group whose shared archive *is* their cross-user search
surface is served by regenerating fingerprints into a local, disposable index
from the archived rxnfiles at import time.

`ReactionSmiles` is included despite being derivable from the rxnfile: it is a
short text string a human can read, grep, diff or paste into any web tool with
no chemistry parser involved. One Indigo call at write time.

**RInChI is excluded.** It requires the InChI Trust's separate RInChI library —
a new third-party binary with its own versioning — and its value is canonical
exact-reaction identity, which the two InChIKeys already provide for the step
while the rxnfile remains the structure of record. Marginal benefit against a
real dependency cost.

### 3.5 Record files

```
<ArchiveRoot>\
    _ReadMe.txt                     format description; marks the root
    2026\
        SAB\
            SAB-00042_v1.pdf        PDF/A-3b, payload embedded   (canonical)
            SAB-00042_v1.json       same payload, plain          (canonical)
            SAB-00042_v1.sha256     sha256sum format, both files (commit marker)
        MRW\
            ...
    _volumes\
        OrgArchive_2026.sqlite      generated, blob-free, read-only
        OrgArchive_2026.sha256
```

Six properties worth being explicit about:

- **Names are globally unique without reference to the folder.**
  `ExperimentID` is already `<UserID>-00042` (`GetNextExperimentID`), and
  `ExperimentPrint` already names exported PDFs after it. The `_v<n>` suffix is
  the finalize counter of section 4.2. Collisions between users are impossible
  by construction, so the user subfolder is organisation, not identity.
- **The `.sha256` file is the commit marker.** Write the PDF and JSON first,
  under temporary names, then rename them into place, then write the checksum
  file last. A scanner ignores any record whose checksum file is absent. This is
  what gives a filesystem the atomic coupling of document and metadata that a
  transaction gives a database — the usual objection to a filesystem tree. A half-published record is invisible rather than corrupt, and
  the next publish attempt simply completes it.

  The file serves two roles, and only the first needs its contents. Its
  *existence* commits the record; its *contents* allow a corruption check, so a
  later reader can tell whether the bytes are still the published ones (6.1).
  An empty
  marker would commit just as well, but the record would then be unverifiable
  for the rest of its life, and the hash costs nothing: the same digest is
  computed anyway to populate `PdfSha256` in a volume row.
- **Publication is atomic per file.** Write to `<name>.tmp` in the destination
  directory, flush, then rename. Rename within one volume is atomic on NTFS and
  over SMB, so a reader never observes a partial file under its final name.
- **The checksum file is in `sha256sum` format**, so verification works with
  standard tools and no Phoenix code.
- **Paths are relative to `<ArchiveRoot>`**, and the root is identified by
  `_ReadMe.txt`. The whole tree can be moved, copied to another share, burned to
  optical media or restored under a different drive letter without invalidating
  anything.
- **Depth is fixed at two levels and path length is bounded.** The original
  objection to filesystem trees — "naming drifts, shares move, path lengths
  bite, one reorganisation severs documents from whatever indexed them" — is an
  objection to *human-maintained* conventions. This layout is program-generated,
  never hand-edited, verifiable by a scan, and its index is derived, so severing
  is not a failure mode that exists.
- **The highest `_vN` is the current record.** Stated in `_ReadMe.txt`, and the
  only convention a human browsing the tree has to know. Nothing is moved,
  renamed or marked when a version is superseded — the archive stays write-once
  — and the folder is single-version for all but the few experiments that are
  ever reopened. Queries do not surface the older versions at all; see 3.6.

### 3.6 Volume schema

One record table. The volume is an index: discardable without touching the
archive.

```sql
CREATE TABLE ArchivedExperiments (
    ArchiveRowID       INTEGER PRIMARY KEY,   -- surrogate, local to this volume
    ArchiveFormatVer   INTEGER NOT NULL,      -- per row, not per file
    ExperimentID       TEXT    NOT NULL,      -- indexed, NOT unique
    ExperimentVersion  INTEGER NOT NULL,      -- finalize counter; see 4.2
    IsSuperseded       INTEGER NOT NULL,      -- set on the prior row
    ArchivedAt         TEXT    NOT NULL,

    UserName           TEXT,
    ProjectTitle       TEXT,
    ProjectFolder      TEXT,
    Tags               TEXT,
    CreationDate       TEXT,
    FinalizeDate       TEXT,

    ReactantInChIKey   TEXT,
    ProductInChIKey    TEXT,
    IsRacemicReactant  INTEGER,               -- required; see 4.2
    IsRacemicProduct   INTEGER,
    InChIVersion       TEXT,

    Yield              REAL,
    Purity             REAL,
    RefReactantGrams   REAL,
    RefReactantMMols   REAL,

    MDLRxnFileString   TEXT,
    ReactionSmiles     TEXT,
    ProtocolText       TEXT,                  -- FTS5 source
    ExperimentJson     TEXT,                  -- queryable via json_extract()
    PayloadSha256      TEXT,

    PdfRelPath         TEXT NOT NULL,         -- relative to <ArchiveRoot>
    PdfSha256          TEXT NOT NULL,
    PdfByteCount       INTEGER NOT NULL
);
```

Four deliberate choices:

- **No document blob.** `PdfRelPath` replaces it. This is what keeps a volume to
  tens of megabytes for a group-year rather than tens of gigabytes — small
  enough that a client copies it locally, or reads it straight off the share.
  Keeping documents out also avoids the file-size, backup and blast-radius costs
  that no amount of column ordering inside the database can address.
- **`ExperimentJson` as TEXT, not BLOB.** SQLite's built-in JSON functions make
  `json_extract(ExperimentJson, '$.products[0].batchId')` work with no schema
  change, so future payload additions need no migration.
- **`ArchiveFormatVer` per row.** An archive accumulates across application
  versions; per-row versioning lets a later reader handle older records
  correctly.
- **Append-only versioning.** `dlgReopen` allows Finalized → Unlocked →
  Finalized. Keying on `ExperimentID` alone would overwrite, silently discarding
  a version someone may already have cited. `ExperimentVersion` / `IsSuperseded`
  keeps history recoverable; a reopened-and-changed experiment becomes visible
  rather than invisible. In the file tier the same property is free: `_v1` and
  `_v2` are separate write-once files, and keeping them needs no mechanism at
  all — re-finalization is an owner-local event that publishes the next version
  through the ordinary path (4.1).

  **Versions must not create duplicate hits.** Searches filter
  `IsSuperseded = 0` by default, so a query returns one row per experiment: the
  current one. A hit whose experiment has earlier versions carries a quiet
  indicator ("v2 — 1 earlier version") and an opt-in toggle reveals them. The
  two audiences want opposite things and both are served, because the version is
  in the filename and the filter is in the index: the chemist locating an
  experiment gets the final record, while a third party doing forensics can see
  the history.

  Overwriting was considered and rejected on three grounds. It is the only
  operation in the design that would mutate an already-published file, so it
  needs overwrite semantics on a share that is otherwise write-once; it breaks
  "a published record is backed up once, ever" (5.2), leaving versioned backup
  targets holding divergent copies under one name; and its confusion mode is
  worse than the one it avoids, because someone who cited `_v1` would find a
  document of the same identity whose content silently differs from what they
  cited. Two labelled versions explain themselves. An overwritten record
  misleads. Where the work falls under GLP/GMP or supports a regulatory
  submission, retention of prior versions is expected rather than optional; but
  the asymmetry decides it regardless of whether that ever applies — keeping
  versions costs one extra document for the minority of reopened experiments,
  while discarding them cannot be undone later.

Auxiliary:

```sql
-- provenance of this volume; it is derived, so this is all it needs
CREATE TABLE VolumeInfo (
    ArchiveYear     INTEGER NOT NULL,
    RowCountTotal   INTEGER NOT NULL,
    GeneratedAt     TEXT    NOT NULL,
    GeneratedBy     TEXT,            -- host/user that built it
    SourceRootHint  TEXT             -- informational only; paths stay relative
);

-- Self-documentation: the published schema and format description live
-- INSIDE the file, mirroring _ReadMe.txt, so neither artefact depends on an
-- external specification surviving. States that this file is derived and how
-- to regenerate it.
CREATE TABLE _ReadMe ( Topic TEXT, Content TEXT );
```

Plus an FTS5 index over `ProtocolText`. FTS5 supports incremental inserts, so
generation is a single pass with no rebuild step; damage to the index is a
non-event, because the whole volume is regenerable.

### 3.7 Payload contents

`ExperimentJson` holds the experiment's data as a readable and queryable record.
It is scoped to what a future reader searches, reads or cites — not to what a
future application would need in order to rebuild live state (1.4):

- `reaction`: the MDL rxnfile, reaction SMILES, reactant and product InChIKeys,
  racemate flags, InChI version
- `steps`: the protocol in presentation order, one entry per step, each with a
  `role` of `refReactant`, `reagent`, `solvent`, `auxiliary`, `comment`,
  `table`, `separator`, `image` or `file`, and the values belonging to that
  role — name, source, molecular weight, grams, millimoles, equivalents,
  density, purity, resin load, InChIKey, and molarity for reagents. Comments
  carry their text. `image` and `file` steps carry no values: they are
  placeholders that keep the step sequence aligned with the document, and the
  content they stand for is in the PDF
- `products`: name, grams, molecular weight, yield, exact mass, elemental
  formula, purity, resin load, batch ID
- `metadata`: experiment ID, version, user, project and folder, creation and
  finalize dates, tags
- `ArchiveOrigin`: how this record came to exist — `"finalize"`, `"backfill"` or
  `"backfill-from-server"` — plus the publishing user and host, and the date.
  Needed because a backfilled record's `_v1` means "state at backfill time", not
  "first finalization": the finalize-counter migration defaults every
  pre-existing finalized experiment to 1, so any re-finalization that happened
  before archiving was activated is not recoverable and must not be implied.
  For a record published on another user's behalf (4.4) it also records that the
  document was rendered on a machine other than the owner's, which is why
  pagination may differ from what the owner would have produced

Attachments appear nowhere in the payload, neither as content nor as metadata.
Inserted documents are already embedded in the PDF/A-3b, so they are stored
exactly once, inside the document, and `PdfSha256` covers them.

Two properties of this list matter as much as its contents.

**The names are the record's own, not the live schema's.** Field and section
names are descriptive, so the payload explains itself to a reader with a text
editor and no knowledge of Phoenix — the second reader path in 3.8. It also
decouples the archive from schema evolution: renaming a table or a column in
the live model must not change what published records look like, and
`ArchiveFormatVer` is then the only thing that governs payload shape.

**Identity and machinery columns are excluded.** Entity `GUID`s, the
`UserID`/`ProjectID`/`ProjFolderID` foreign keys, `SequenceNr`, `SyncState`,
`DisplayIndex` and presentation flags such as `IsRotated` carry no archival
meaning: nobody searches them, they cannot be interpreted without the live
model, and a future reader could easily mistake them for significant data. This
exclusion has to be stated because it is not the default — a payload generated
the way `ExportExperiment` serializes an entity graph would include every one of
them. Step order is preserved by the order of the `steps` array itself, so
`SequenceNr` is not needed to represent it.

### 3.8 Defensible capability claim

What the archive supports with no Phoenix and no chemistry toolkit:

| Capability | Available | How |
|---|---|---|
| Read the documents | Yes | Any PDF/A viewer; files have meaningful names |
| Read one experiment's data | Yes | Open the `.json` sidecar in a text editor |
| Detect corruption | Yes | `sha256sum -c` against the `.sha256` files; corruption only, not tampering (6.1) |
| Query metadata, yields, scales, dates | Yes | Plain SQL against a volume |
| Full-text search protocols | Yes | FTS5, or `LIKE` on `ProtocolText` |
| Identify the current version | Yes | Highest `_vN`; `IsSuperseded = 0` in a volume |
| Exact reaction lookup | Yes | InChIKey equality |
| Trace one step by InChIKey | Yes | SQL self-join, `ProductInChIKey = ReactantInChIKey`; single step only, not a route graph |
| Rebuild the index from scratch | Yes | Scan the sidecars; no PDF parser needed |
| Reaction substructure search | No — needs a toolkit | Rebuild fingerprints from `MDLRxnFileString` |

Rebuilding an experiment as live, editable state is absent from the table on
purpose. It is migration, not archival, and it belongs to the running
application rather than to the preservation record (1.4).

Note the two independent reader paths, which is the deliberate answer to
"what skills will exist in fifty years". A future reader with SQL skills opens a
volume. A future reader with none opens a folder of named files and reads JSON
in a text editor — and can see what the archive contains *before* investing in
any tooling. Neither path depends on the other, and the file path degrades one
record at a time where a container can fail at its header and yield nothing.

Claim wording that survives scrutiny:

> The archive is a folder tree in which each finalized experiment is one
> write-once PDF/A-3b document plus a plain JSON record holding the reaction as
> an MDL rxnfile and reaction SMILES, the full protocol, and the indexed
> properties, with SHA-256 checksums. A generated SQLite volume per year
> provides indexed and full-text search over those records and can be rebuilt
> from them at any time. Documents, metadata, protocol text and search are
> accessible with standard tools and no Phoenix installation.

Specifically **not**: "an archive you can run reaction substructure searches
on." Rather: the archive preserves everything needed to rebuild reaction
substructure search with any cheminformatics toolkit, and Phoenix searches it
directly today.

## 4. Lifecycle

### 4.1 Write trigger

Finalization. The event already exists and already gates RSS indexing. On
WorkflowState → Finalized: render the PDF/A-3b, build the payload, write the
record files into the local pending folder. One experiment, one record, three
files, published as described in 3.5.

### 4.2 Required live-model change

`tblExperiments` needs a **finalize counter**, incremented on each finalization,
to populate `ExperimentVersion`, to name record files, and to support the gap
diff in section 7.2. Not new information — the archive record needs it
regardless. No such column exists today.

The racemate flags must be carried into the archive: `SequenceStep` uses
`IsReactantRacemate` / `IsProductRacemate` in step matching, so a naive join
without them produces subtly wrong edges for racemic cases. They are the
difference between derivable and misleading.

### 4.3 Local pending folder

The client-side staging area is a **transient outbox**, not a permanent copy,
and a plain folder rather than a database. Records are written there on
finalization and moved to the share on the next successful publish attempt;
confirmed records are deleted locally.

It lives in the `Phoenix ELN Data` folder, so `PackElnData` carries pending
records through a machine migration at no cost.

Deleting confirmed records is safe because the archival record is regenerable
from the live database. One caveat worth recording in `_ReadMe`: a re-rendered
PDF will not be byte-identical (font availability, library versions), so a
*published checksum* cannot be reproduced after regeneration — only the record's
content.

### 4.4 Activation and backlog

A user may hold several hundred finalized experiments when archiving is first
switched on. Without a backlog pass the capability claim of 3.8 would be false
on day one — "the archive holds the finalized experiments" would silently mean
"the ones finalized after the upgrade". So the backlog is not an enhancement; it
is a precondition of the claim.

**It needs no separate mechanism.** The diff of 7.2 *is* the backlog: at
activation its result is "everything", in steady state "nothing". Same query,
same publish path, same idempotency, same resumability after a crash — there is
no progress state to persist, because the diff is recomputed. That a backlog
required no new machinery is reasonable evidence the design is sound.

What *is* new is pacing, because the initial set is large.

#### Pacing: rendering off the UI thread, publication jittered

Rendering runs on a **dedicated STA thread** with its own `Dispatcher` and its
own `DbContext`, and does not involve the UI thread at all. This was built and
demonstrated rather than assumed: a three-page record renders in about 1.2 s.

Two constraints on the render path are worth recording, since neither is
enforced by the compiler and both are easy to reintroduce:

- A detached visual tree must not broadcast on the `Shared` events that
  `CustomControls` uses as its intra-UI notification bus. Suppression belongs at
  the raise site and must be per instance — filtering inside each subscriber
  breaks whenever a subscriber is added, and a process-wide flag cannot express
  "this render" and races once rendering has its own thread.
- The XPS package URI must be unique per render, because `PackageStore` is
  process-global and releases a registration only on `Close`.

Pacing survives, but its reason is not UI responsiveness — it is the share.
Activating archiving across an organisation of fifty clients, or rolling out
the version that enables it, would otherwise have every client begin its
backfill in the same second and saturate the share with concurrent writes.

- **Jitter the start.** Each client delays its first backlog pass by a random
  interval drawn over hours, so activation spreads instead of synchronising.
- **Jitter the interval**, rather than publishing at a fixed rate. A fixed rate
  phase-locks clients that happened to start together; a randomised interval
  pulls them apart.
- **Separate the two costs.** Rendering is local CPU; publication is network.
  They can be paced independently — a client may render a batch and then release
  records to the share gradually.
- **Back off on share errors** rather than retrying immediately, so a share
  under pressure is not pressed harder.
- **Defer on battery**, so a laptop does not spend its charge on backlog work.

Interference hazards that remain in the existing export code:

- **`New PrintDocument()` and the printer-settings query** must be hoisted out
  of the loop and cached per session, as the bulk path already does. A faulty
  driver made exactly this call cost seconds.
- **Re-entrancy.** One queue, one worker, an in-progress guard. A new
  finalization takes priority but never runs concurrently.

The render path itself raises no dialogs: it reports a `PdfRenderResult`, and
any user-facing message is the caller's, on the UI thread. That is a
precondition for background rendering, not a stylistic choice — a modal raised
from a worker thread would block a render nobody is watching.

#### Scope and order

Scope is **every user present in the local database**, not just the signed-in
one — `DBContext.tblUsers.ToList()` shows a local store can hold several.
Filenames carry the `UserID`, so this changes nothing about collisions.

Order is **newest-first**. Recent work is what gets searched, cited and shared;
the backlog boundary then moves monotonically backwards instead of interleaving
confusingly with new finalizations. Older records are no less safe in the live
database meanwhile.

#### Server-driven backlog

Local-only backfill cannot reach the records of a user who has already left and
whose machine is gone — and at activation that is *every* past member of the
group, potentially years of history. The server holds them, so the server is the
work queue:

- Each connected client drains items the server reports as unarchived,
  **regardless of owner**, and marks them archived when the files are confirmed
  on the share.
- `ServerDBContext` is a full `ElnDbContext` over MySQL with lazy-loading
  proxies, and `SetPrintTemplate` only walks navigation properties, so a foreign
  user's experiment renders **directly from the server context** — no restore
  into `ELNData.db`, no local bloat. The `DisplayIndex = -3` foreign-user
  preview is precedent that the graph loads correctly this way. Use explicit
  `Include` chains rather than lazy loading, or each experiment becomes dozens
  of round trips.
- Files are named and filed under the **owning** user, not the publisher.
- `ArchiveOrigin` records the publisher, so a record rendered on someone else's
  machine says so (3.7).

No new confidentiality exposure: server-mode search and the foreign-user preview
already let any client view any user's experiment.

#### Queue state: two server-only columns

```sql
-- on the SERVER's tblExperiments only; deliberately NOT in ElnCoreModel
ArchivedVersion   INT      NULL,   -- highest FinalizeCount confirmed on the share
ArchiveClaimedAt  DATETIME NULL    -- in-flight marker, for arbitration only
```

Four decisions here, each load-bearing:

- **Outside the EF model, read and written by raw SQL on the server
  connection.** `ServerSync.CloneSyncEntity` does
  `CurrentValues.Clone()`/`SetValues`, and `RestoreFromServer.AddEntity` does
  `.Entry(localEntity).CurrentValues.SetValues(serverEntity)`. Both copy *every
  mapped property wholesale*; neither has a column allowlist. A mapped property
  would therefore ride both sync and restore, and excluding it would mean
  re-clearing it after every `SetValues` call forever, with no compile-time
  signal. Keeping it out of the model makes the exclusion structural: EF cannot
  touch what it does not know about. (Sync is driven entirely by the client
  scanning its *local* `SyncState <> 0`; the server holds no triggers and
  initiates nothing, so there is nothing else to opt out of.)
- **A version, not a boolean.** The drain condition is
  `WorkflowState = 1 AND (ArchivedVersion IS NULL OR ArchivedVersion <
  FinalizeCount)`. The finalize counter of 4.2 is real record data and therefore
  syncs, so the server knows it. A version self-corrects in two cases a flag
  misses — a re-finalized experiment, and an owner whose client died between
  incrementing the counter and publishing — and needs no reset logic anywhere,
  hence no coupling into `dlgReopen`.
- **A claim registry, not a ledger.** 7.2 rejected a publish ledger and that
  still holds, because these columns are never treated as truth. Truth remains
  "the record file exists on the share and its hash matches". The columns do one
  job the share cannot: arbitrate which client renders which experiment, so
  several clients do not each spend multi-second UI-thread renders on the same
  document. They are prunable — clear them and the worst outcome is redundant
  work, which idempotent publication absorbs — and a periodic reconcile
  (enumerate the share, diff against the server's finalized set) corrects any
  drift, so a wrong value can never hide an unarchived experiment.
- **Claims are taken atomically** by conditional update with an affected-rows
  check, with a staleness interval expiring claims from clients that died:

  ```sql
  UPDATE tblExperiments SET ArchiveClaimedAt = NOW()
   WHERE ExperimentID = ? AND WorkflowState = 1
     AND (ArchivedVersion IS NULL OR ArchivedVersion < FinalizeCount)
     AND (ArchiveClaimedAt IS NULL OR ArchiveClaimedAt < NOW() - INTERVAL 30 MINUTE)
  ```

  Arbitration is load-bearing rather than an optimisation, because 5.3's
  collision-freedom argument fails the moment clients publish foreign records:
  two clients could both target `2024\MRW\MRW-00123_v1.*`, and the concrete
  failure is interleaving — one client's `.sha256` landing beside the other's
  PDF, producing a hash mismatch that looks like corruption but is not. The
  6.1 sweep repairs it, but a false signal in the one channel that must stay
  trustworthy is worth a `DATETIME` to avoid. This is also the one thing a bare
  share could not do and MySQL does correctly for free.

Deliberately **not** recorded: which client archived which record, when it
claimed the item, how long it took. That is operational telemetry, not record
content; it belongs in the log.

#### Owner-first

Cross-user rendering moves data twice — the experiment and its attachments down
from MySQL, then the document up to the share — when the owning client usually
holds everything on local disk already. With large embedded attachments,
draining a long backlog that way could move hundreds of gigabytes.

So bias the queue: a client drains **its own users' records first, from the
local database**, and picks up another user's record only after that record has
sat unclaimed for a grace period of about a week. The common case stays cheap,
and cross-user rendering is reserved for genuinely orphaned records — which is
the case it exists to solve. A departed user's experiments pass the grace period
immediately.

#### Completeness state

Because a backlog pass can span days, the application must be able to answer
*"is this user's archive complete?"*. This is load-bearing twice: 7.4's
offboarding capture is worthless if nobody can tell whether the departing
person's records were published before the laptop is wiped, and the claim of 3.8
needs a per-user basis in fact.

Expose the pending count, the last successful publish, and a per-user "complete
as of \<date\>" that an offboarding checklist can read. Add an **"Archive
outstanding experiments now"** command — the same queue with pacing switched
off and a progress dialog, reusing the existing `PdfExportProgress` and
`PDFExportCompleted` events. The automatic path guarantees eventual completion;
the manual one serves whoever wants it finished tonight.

#### Without a server

The fallback is the local share diff of 7.2 and needs no queue state at all,
because there is only one writer. Adding a server later reconciles
automatically: the first server-connected session enumerates the share, finds
what is already present, and queues only the genuine gap.

What this cannot recover is a pre-activation user who never synced to a server
and whose machine is gone. Nothing addresses that case.

## 5. Organisation-level aggregation

### 5.1 Layering

Two systems, two jobs:

- **MySQL/MariaDB — live aggregation.** Concurrent multi-user access,
  continuous sync, org-wide RSS and full-text search *now*. Already built.
- **The archive — preservation.** Open-format files that open with no server, no
  DBA, no credentials, no institution.

**The archive must not be a MySQL table.** A MySQL dump presumes a compatible
MySQL exists to restore into and a person motivated to do so — precisely the
dependency the archive exists to eliminate. MySQL's problem here is not reader
skills; DBAs will exist in fifty years. Its problem is *custodial continuity*: a
running service, credentials, a host, and an unbroken upgrade path across five
decades of major versions.

MySQL remains available as an optional *second* write path for installations
that run it — rows into a table, with a client periodically exporting the file
tree and volume — but it can never be the only path, or server-less groups lose
archiving entirely.

### 5.2 Yearly partitioning

Records live under `<ArchiveRoot>\<year>\<userID>\`, and **only the current
year's folder is ever written to**. Consequences:

- A year of a five-chemist group is a few hundred records per user folder —
  unremarkable for NTFS, where enumeration costs only start to bite above
  roughly a hundred thousand entries in a single directory. The concern is real
  in principle and dissolved by partitioning the design already wanted.
- Prior years are complete and can be set read-only at the filesystem level,
  moved to slower or write-once storage, or archived to optical media
  independently.
- Volumes are per year, so the current-year volume is the only one ever
  regenerated in normal operation.
- Backups become write-once per record: a published record is backed up once,
  ever, and never re-copied. This is the property a single mutable container
  cannot have.

### 5.3 Transport

On each attempt the client publishes its pending records to the current-year
folder, verifies, then deletes them locally, retrying until it succeeds.

Transfer cost scales with *new* data only, which was the original requirement.
Nothing accumulated is ever re-transferred: a record is copied once and a frozen
year is never touched again. Volume regeneration is the one exception and is
addressed in 5.5.

Verification before clearing the pending folder: re-read the published files,
recompute both hashes, compare against the `.sha256`. Do not trust a copy's
return value. A lost or torn write then self-heals on the next attempt instead
of being silently dropped.

Jittered retry on finalize, on startup, and on a backoff timer, so multiple
clients do not poll in lockstep. No lock file is needed for record publication:
filenames are derived from the *owning* user's `UserID` and the finalize
counter, so for its own records a client is the only possible writer. Where a
client publishes on another user's behalf (4.4) that guarantee comes instead
from the server-side claim, which is why arbitration there is required rather
than merely efficient.

### 5.4 The trilemma, resolved

Sharing an archive over SMB looks like a choice of any two of:

1. No whole-file transfer (incremental traffic)
2. No SQLite writes over SMB (no corruption exposure)
3. No process running on the server/NAS (no extra tools)

The conflict is a property of a shared *mutable database*, not of the problem
itself, so publishing write-once records satisfies all three at once: record
publication is incremental (1), writes nothing but complete files (2), and needs
nothing running anywhere (3).

### 5.5 Volume generation and publication

A volume is built **locally** and published as a whole file. Never written in
place over the share.

- **Trigger:** on demand, at year end, and opportunistically — the first client
  of the day may regenerate if the record count has advanced beyond the volume's
  `RowCountTotal`. There is no correctness requirement to regenerate at any
  particular time; a stale volume is a stale index, not a damaged archive.
- **Build:** scan `<year>\*\*.sha256` for committed records, read the `.json`
  sidecars, insert one row per record, build FTS5 in the same pass. No PDF
  parsing. For a group-year this is thousands of small reads — seconds locally,
  a few minutes over a share on a cold full rebuild.
- **Publish:** write `OrgArchive_<year>.sqlite.tmp` beside the target, rename
  over the old one, then write the matching `.sha256`. A single stale reader
  holding the previous file open is harmless, since the old file remains a valid
  index until it is released.
- **Read:** frozen volumes are read-only and can be opened lock-free over a
  share by passing `immutable=1` in the connection URI, which disables locking
  and change detection entirely, so any number of readers work concurrently.
  Keep volumes out of WAL mode and read-only at the filesystem level so the
  promise is true. Clients may equally copy a volume locally — it is small
  enough, and that is what makes a newcomer's first search fast without
  enumerating thousands of sidecars.
- **Current-year search** runs against the client's own incrementally maintained
  index, which already covers that client's experiments and picks up others'
  records as they are published. The published volume is the bootstrap and the
  cross-year surface, not the live hot path.

If hash chaining is ever added for tamper evidence, it must be computed over the
canonical order (`year`, `UserID`, `ExperimentID`, `ExperimentVersion`) at
volume generation time, not at record write time — records are published
concurrently by independent clients and have no global write order. Per-record
hashes are unaffected.

## 6. Integrity

### 6.1 Per-record corruption check

The `.sha256` file beside each record is the whole mechanism. It covers both the
document and the payload, it is written once, and it is checkable with standard
tools.

**What it guarantees, stated precisely.** It is a corruption check: it detects
*accidental* change — bit rot, a truncated copy over SMB, a backup restored from
a set that silently dropped bytes, decaying optical media. These are the
failures an archive actually meets over decades, and a checksum is the only
thing that catches them.

It is **not** tamper evidence. A checksum is not a signature: anyone with write
access to the share can modify a record and recompute its hash, and nothing in
the archive would reveal it. The design deliberately does not claim otherwise.
Tamper evidence, if it is ever required — a regulatory submission, a patent
dispute — needs a mechanism this design does not currently include:

- share ACLs that grant modify to nobody once a record is published, making the
  file tier write-once by permission rather than by convention
- a copy onto WORM or object-lock storage, which the file tree supports
  unchanged (section 8)
- digitally signed PDFs, which Phoenix has no code for today
- hash chaining across records (5.5), which detects alteration but still proves
  nothing about *who* altered it

The first of these is cheap and worth doing regardless, since it also prevents
the accidental overwrite that the write-once model assumes cannot happen.

- **On publish:** read back and compare, before clearing the pending folder
  (5.3).
- **On open:** verify the document hash when a user opens an archived PDF. Cheap
  relative to rendering it, and it is the moment the answer actually matters.
- **On schedule or on demand:** a background sweep over a year folder, comparing
  every record against its checksum. Mismatches and missing files are reported,
  and both are repaired by republication (7.2).

Structural database checks — `PRAGMA quick_check`, `integrity_check` — apply only
to volumes, where the answer never matters: a volume that fails any check is
deleted and regenerated.

### 6.2 Why no verification apparatus is needed

A shared mutable database on the share would need a daily first-client-of-the-day
pass that takes a lock, runs `quick_check`, and copies the volume into a rotating
known-good backup set, precisely so that corruption cannot propagate into
backups. A write-once share needs none of it:

- Nothing on the share is mutable, so corruption has nothing to propagate
  *through*.
- Record files are write-once, so a backup set of them is known-good by
  construction rather than by verification.
- Volumes need no backup at all, because they are regenerable.
- A `VolumeManifest` regression tripwire, an orphaned-journal check and a
  stale-lock check would each watch for a symptom of interrupted database writes
  over a share. There are no such writes.

Ordinary filesystem backup of `<ArchiveRoot>` — any tool, incremental, including
to write-once media — is the entire backup strategy.

## 7. Regeneration instead of recovery

### 7.1 No recovery path exists, by construction

There is no step in this design that requires recovering a damaged SQLite file,
and no code path, user-facing workflow or shipped binary for doing so.

Recovery of a container only ever buys one thing: salvaging the *original* copy
of rows that cannot be obtained from anywhere else. In this design that set is
empty:

| Damaged artefact | Response |
|---|---|
| A record file | Republish it from the live database (7.2) |
| A volume | Delete and regenerate from the record files (5.5) |
| The local pending folder | Re-render from the live database |
| The local search index | Rebuild from a volume or by scanning records |

Every one of those is automatic, uses only the standard API, and needs no
specialist.

### 7.2 Regeneration needs no ledger

```
records to republish =
    { my Finalized experiments in ELNData.db, by (ExperimentID, FinalizeCount) }
  − { committed record files under <ArchiveRoot>\<year>\<my UserID>\ }
  − { records present but failing hash verification }   ← these are overwritten
```

**The live database is the ledger.** A separate publish-ledger table was
considered and rejected: it would duplicate information already derivable, and
could itself go stale or wrong. The pending folder covers in-flight records; the
live database covers history.

The diff works whether the destination is empty, partially damaged, or intact,
which is why no knowledge of what was previously transferred is required. It is
idempotent, it runs in the background, and an interrupted run is simply
continued on the next attempt. Republishing a hash-mismatched record is the one
case that overwrites a file on the share, and it is the one case where
overwriting is correct.

The error asymmetry favours triggering this aggressively on weak evidence. A
false positive costs a redundant file copy. A false negative leaves a silent gap
in an archive, the one outcome that cannot be tolerated.

### 7.3 Salvage: documented, not implemented

Kept here only so that a future reader of a damaged file is not left guessing.
`_ReadMe` should carry a short version of this section.

If a SQLite file ever does need salvaging, three tiers exist:

1. **Standard API only, fully automatable.** `PRAGMA integrity_check` /
   `quick_check` for triage and `PRAGMA cell_size_check=ON` to fail early;
   `REINDEX` for index-only damage; `INSERT INTO ft(ft) VALUES('rebuild')` for
   FTS5. For table damage, a **rowid probe loop**: because
   `ArchiveRowID INTEGER PRIMARY KEY` *is* the rowid, iterate the known id range
   and run `SELECT … WHERE ArchiveRowID = ?` inside a try/catch, skipping rows
   that raise `SQLITE_CORRUPT`, and insert the survivors into a fresh database.
   Each probe descends the B-tree independently, so a broken subtree costs only
   the rows beneath it, and no table scan — the operation that dies on a damaged
   interior page — is ever needed. Roughly forty lines through any binding.
2. **The `sqlite3recover` extension**, which is what the shell's `.recover` is
   built on, and which is a real C API rather than shell-only magic. It is a
   compile-time extension, not part of the default amalgamation, and is not
   present in `winsqlite3.dll` — which is what Phoenix binds to via
   `SQLitePCLRaw.bundle_winsqlite3`. Using it would mean building and shipping a
   custom native SQLite.
3. **`sqlite3.exe .recover`**, whose unique capability over tier 1 is page-scan
   recovery of rows orphaned from the B-tree entirely, and schema salvage when
   `sqlite_schema` itself is damaged.

Tier 1 covers everything reachable through an intact B-tree path and needs no
CLI, no extension and no expertise. Tiers 2 and 3 address only the rarest damage
classes — and in this design they have **no remaining use case at all**, because
the only SQLite files are a derived volume and a local index, both regenerable
in less time than any salvage attempt would take.

### 7.4 The one irreducible manual dependency

Records belonging to someone who has since left, whose live database is gone,
where the records were never published and backups do not reach far enough.

Activation widens this from "whoever leaves next" to **everyone who has already
left**, which is why the server-driven backlog of 4.4 exists: for any former
member who ever synced, the server still holds their experiments and any client
collects them. What remains irreducible is narrower — a user who *never* synced
to a server and whose machine is gone.

This is not a recovery problem and no tool addresses it. It is otherwise bounded
by two facts: a record is published within seconds of finalization rather than
at some later batch point, and all prior years are complete, read-only and
backed up. Capturing a departing user's data folder closes the remaining window,
and the completeness state of 4.4 is what makes it checkable before the machine
is wiped.

That capture belongs on the organisation's offboarding checklist next to
"collect laptop" — an existing procedure someone already follows, rather than a
new habit someone has to form. It is also the single most common way research
data is actually lost, and the only archival trigger that genuinely resists
automation.

## 8. Rejected alternatives

| Option | Why not |
|---|---|
| **A shared SQLite file written to over SMB** | Advisory locking over SMB is unreliable, caching can acknowledge non-durable writes, and WAL is impossible. Containing the risk required a lock protocol, tripwires, verified backups and an automatic recovery sequence — all of which disappear once the share holds only write-once files |
| Document blobs inside the queried database | Past SQLite's ~100 KB blob crossover by orders of magnitude; forces full-file backups, unbounded file growth, and container-wide blast radius for no query benefit |
| MySQL archive table as the archive | A dump presumes a compatible MySQL and a motivated operator. Its weakness is custodial continuity, not reader skills. Retained as an optional second write path only |
| Payload *only* inside the PDF | Extraction then requires understanding PDF/A-3b embedded-file structure — a genuine specialist step. The plain JSON sidecar exists precisely to remove it |
| Indigo fingerprints / `RxnIndigoObj` in the archive | Opaque and version-locked. Regenerable from the archived rxnfiles into a local index, with better technology than exists today |
| RInChI | New third-party binary dependency for marginal benefit over the two InChIKeys |
| Separate publish-ledger table | Information already derivable from the live database. The server-side queue columns of 4.4 are claim arbitration, never treated as truth |
| A separate `tblArchiveClaims` table | Two columns on the server's `tblExperiments` do the same job without a table, without history, and without anything to prune |
| Queue state as a *mapped* EF property | `CloneSyncEntity` and `AddEntity` copy all mapped properties wholesale, so it would ride both sync and restore; exclusion would depend on discipline rather than structure |
| Queue state as a boolean `IsArchived` | Cannot express re-finalization, so it needs a reset hooked into `dlgReopen` with no compile-time signal. `ArchivedVersion < FinalizeCount` self-corrects |
| Overwriting the archive on re-finalization | The only operation that would mutate a published file; breaks backup-once; and silently diverges from a version that may already have been cited. See 3.6 |
| Fixed-rate backlog throttling | A fixed rate phase-locks clients that start together, which is the opposite of what is needed when many clients activate at once. Randomised intervals pull them apart (4.4) |
| Scheduled or manual merge of whole archive files | Transfer cost scales with accumulated rather than new data; reintroduces the human factor |
| A helper process or service owning the archive file | Solves concurrency correctly, but is the installed, operated component the design exists to avoid |
| Object storage with versioning and object lock | Genuine WORM semantics and a good fit for write-once records, but adds credentials and a vendor dependency. The file tree can be *copied* to it unchanged, so this stays an option rather than a requirement |
| Consumer sync folders (OneDrive, Dropbox) as the share | Actively hostile to a database file — conflict copies, delayed upload, locked files — but perfectly safe for write-once records, which is a further argument for the file tier |
| Append-only ZIP, git, git-annex as the container | Same concurrency exposure, worse tooling, binary bloat |
| Stripping the payload from archived PDFs to avoid duplication | Costs the document self-sufficiency once separated from the archive |
| A sidecar index that is *authoritative* | The original and correct objection to filesystem trees. Avoided by making the index strictly derived, so divergence is not a failure mode that can occur |

## 9. Implementation checklist

1. CSV/XLSX export of RSS hits and the Same-Step list (section 1.1) — highest
   value, smallest change, independent of everything else.
2. Finalize counter column on `tblExperiments` (section 4.2).
3. Payload serializer producing `ExperimentJson` + RDfile, shared by the PDF
   attachment, the JSON sidecar and the volume row, with one `PayloadSha256`.
4. Embed the payload in the PDF/A-3b export (section 2).
5. Record writer: pending folder, three files, checksum last (sections 3.5, 4.3),
   including `ArchiveOrigin` in the payload (section 3.7).
6. Publisher: temp-name-plus-rename, read-back verification, jittered retry, no
   lock file (section 5.3).
7. Volume generator: scan committed records, build rows plus FTS5, publish by
   rename (section 5.5).
8. Volume reader: `immutable=1` over the share, or local copy; current-year
   searches against the client's incremental index (section 5.5). Default
   `IsSuperseded = 0` filter plus the earlier-versions indicator (section 3.6).
9. Regeneration pass: the gap diff, including hash-mismatch republication
   (section 7.2).
10. Backlog drain: worker-thread rendering, jittered pacing, the remaining
    interference fixes, all local users, newest-first (section 4.4).
11. Server queue: the two raw-SQL columns, atomic claim, owner-first with grace
    period, rendering foreign experiments from `ServerDBContext` (section 4.4).
12. Completeness state and the "archive outstanding experiments now" command
    (section 4.4).
13. Verification sweep and verify-on-open (section 6.1).
14. `_ReadMe.txt` and the `_ReadMe` table populated — format description, the
    highest-`_vN` convention, regeneration instructions, the salvage note from
    7.3 — and the schema published in the repository.
