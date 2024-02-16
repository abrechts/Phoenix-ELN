using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblProtocolItems
{
    public string GUID { get; set; } = null!;

    public string ExperimentID { get; set; } = null!;

    public short ElementType { get; set; }

    public short? SequenceNr { get; set; }

    public string? TempInfo { get; set; }

    public byte IsSelected { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblExperiments Experiment { get; set; } = null!;

    public virtual tblAuxiliaries? tblAuxiliaries { get; set; }

    public virtual tblComments? tblComments { get; set; }

    public virtual tblEmbeddedFiles? tblEmbeddedFiles { get; set; }

    public virtual tblProducts? tblProducts { get; set; }

    public virtual tblReagents? tblReagents { get; set; }

    public virtual tblRefReactants? tblRefReactants { get; set; }

    public virtual tblSeparators? tblSeparators { get; set; }

    public virtual tblSolvents? tblSolvents { get; set; }
}
