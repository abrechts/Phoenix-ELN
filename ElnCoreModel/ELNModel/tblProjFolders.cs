using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblProjFolders
{
    public string GUID { get; set; } = null!;

    public string ProjectID { get; set; } = null!;

    public string FolderName { get; set; } = null!;

    public short? SequenceNr { get; set; }

    public byte? IsNodeExpanded { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblProjects Project { get; set; } = null!;

    public virtual ICollection<tblExperiments> tblExperiments { get; set; } = new List<tblExperiments>();
}
