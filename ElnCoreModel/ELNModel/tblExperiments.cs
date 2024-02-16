using System;
using System.Collections.Generic;

namespace ElnCoreModel;

public partial class tblExperiments
{
    public string ExperimentID { get; set; } = null!;

    public string UserID { get; set; } = null!;

    public string ProjectID { get; set; } = null!;

    public byte IsDesignView { get; set; }

    public byte IsCurrent { get; set; }

    public string? RxnSketch { get; set; }

    public string? MDLRxnFileString { get; set; }

    public string? ReactantInChIKey { get; set; }

    public string? ProductInChIKey { get; set; }

    public short? WorkflowState { get; set; }

    public short? UserTag { get; set; }

    public double? Yield { get; set; }

    public double? Purity { get; set; }

    public double? RefReactantGrams { get; set; }

    public double? RefReactantMMols { get; set; }

    public double RefYieldFactor { get; set; }

    public string? CreationDate { get; set; }

    public string? FinalizeDate { get; set; }

    public short? DisplayIndex { get; set; }

    public byte? IsNodeExpanded { get; set; }

    public byte? SyncState { get; set; }

    public virtual tblProjects Project { get; set; } = null!;

    public virtual tblUsers User { get; set; } = null!;

    public virtual ICollection<tblProtocolItems> tblProtocolItems { get; set; } = new List<tblProtocolItems>();
}
