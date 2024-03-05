Imports ElnBase

Public Class dlgSearch

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Property DBContext As ElnDbContext


    Private Sub pnlQuerySketch_SketchEdited(sender As Object, skInfo As SketchResults) Handles pnlQuerySketch.SketchEdited

        Dim rxnSub As New RxnSubstructure
        Dim expHits = rxnSub.PerformRssQuery(skInfo.MDLRxnFileString, DBContext)

        hitList.ItemsSource = expHits

    End Sub


End Class
