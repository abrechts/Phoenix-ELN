Imports System.Windows.Controls

Public Class dlgSequences


    Public Shared Event ClearSequenceSelections(sender As Object)


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.

        AddHandler SequenceControl.SequenceSelected, AddressOf SequenceControl_SequenceClicked

    End Sub


    Public Property ConnectionsPanel As Grid
        Get
            Return pnlConnections
        End Get
        Set(value As Grid)
            pnlConnections = value
        End Set
    End Property


    Private Sub Me_PreviewMouseDown() Handles pnlConnections.PreviewMouseDown

        RaiseEvent ClearSequenceSelections(Me)

    End Sub


    Private Sub SequenceControl_SequenceClicked(sender As Object)

        PopulateSequenceStructures(CType(sender, SequenceControl))

    End Sub


    Private Sub PopulateSequenceStructures(sequence As SequenceControl)

        pnlSeqStructures.Children.Clear()

        For Each stp In sequence.SequenceSteps

            Dim seqStruct As New SequenceStructure

            If stp Is sequence.SequenceSteps.First Then
                If sequence.HasParent Then
                    seqStruct.ShowLeftArrow()
                End If
            End If

            seqStruct.StructureCanvas = stp.GetReactantImage
            pnlSeqStructures.Children.Add(seqStruct)

        Next

        'add final product struct structure
        Dim prodSeqStruct As New SequenceStructure
        prodSeqStruct.StructureCanvas = sequence.SequenceSteps.Last.GetProductImage
        If Not sequence.HasChildren Then
            prodSeqStruct.HideRightArrow()   'hide arrow if no more downstream sequences
        End If

        pnlSeqStructures.Children.Add(prodSeqStruct)

    End Sub


End Class
