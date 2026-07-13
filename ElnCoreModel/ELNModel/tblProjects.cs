using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblProjects
{
    public string GUID { get; set; } = null!;

    public string UserID { get; set; } = null!;

    public string? Title { get; set; }

    public short? SequenceNr { get; set; }

    public byte IsNodeExpanded { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblUsers User { get; set; } = null!;

    public virtual ICollection<tblExperiments> tblExperiments { get; set; } = new List<tblExperiments>();

    public virtual ICollection<tblProjFolders> tblProjFolders { get; set; } = new List<tblProjFolders>();
}
