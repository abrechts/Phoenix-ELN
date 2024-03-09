Imports System.ComponentModel
Imports System.Windows
Imports ElnCoreModel
Imports System.Windows.Controls
Imports ElnBase.ELNEnumerations


Public Class SketchAreaRSS

    Public Event SketchEdited(sender As Object, skInfo As SketchResults)

    Public Sub New()

        ' This call is required by the de§signer.
        InitializeComponent()

    End Sub


    Public Shared Shadows ReadOnly ReactionSketchProperty As DependencyProperty =
        DependencyProperty.Register("ReactionSketch", GetType(String), GetType(SketchAreaRSS),
        New PropertyMetadata(AddressOf OnReactionSketchChanged))

    ''' <summary>
    ''' Sets or gets the currently displayed reaction sketch XML.
    ''' </summary>
    ''' 
    Public Property ReactionSketch() As String
        Get
            Return GetValue(ReactionSketchProperty)
        End Get
        Set(ByVal value As String)
            SetValue(ReactionSketchProperty, value)
        End Set
    End Property


    Private Shared Sub OnReactionSketchChanged(ByVal o As DependencyObject, ByVal e As DependencyPropertyChangedEventArgs)

        Dim skArea = DirectCast(o, SketchAreaRSS)

        With skArea

            .SketchInfo = DrawingEditor.GetSketchInfo(e.NewValue)
            If .SketchInfo IsNot Nothing Then
                .DrawReactionSketch()
                .blkClickInfo.Visibility = Visibility.Collapsed
            Else
                CType(.sketchViewbox.Child, Canvas).Children.Clear()
                .blkClickInfo.Visibility = Visibility.Visible
            End If

        End With

    End Sub


    ''' <summary>
    ''' Sets or gets the ChemBytes Draw SketchResults info associated with the current 
    ''' reaction sketch.
    ''' </summary>
    ''' 
    Public Property SketchInfo As SketchResults


    ''' <summary>
    ''' Edit sketch after mouse click
    ''' </summary>
    ''' 
    Private Sub sketchGrid_MouseDown() Handles sketchGrid.PreviewMouseDown

        If SketchInfo Is Nothing Then
            EditSketch("")
        Else
            EditSketch(SketchInfo.NativeReactionXML)
        End If

    End Sub


    ''' <summary>
    ''' Displays the sketch editor after a mouse click.
    ''' </summary>
    ''' 
    Public Sub EditSketch(rxnSketch As String)

        Dim cbDraw As New DrawingEditor(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\Phoenix ELN Data")

        If cbDraw IsNot Nothing Then

            cbDraw.DialogProperties.SketchValidation = EditorOptions.SketchConditions.Reaction

            'CORE
            Dim skInfo = cbDraw.DisplayDialog(rxnSketch)

            'editor not cancelled?
            If skInfo IsNot Nothing Then

                SketchInfo = skInfo
                blkClickInfo.Visibility = Visibility.Collapsed

                DrawReactionSketch()

                RaiseEvent SketchEdited(Me, skInfo)

            End If

        End If

    End Sub



    ''' <summary>
    ''' Places the reaction sketch into its parent ViewBox.
    ''' </summary>
    ''' 
    Private Sub DrawReactionSketch()

        If SketchInfo IsNot Nothing Then

            Dim skCanvas = SketchInfo.SketchCanvas
            sketchViewbox.Child = SketchInfo.SketchCanvas

            Dim shrinkThreshold = 400 '700

            If skCanvas.Height < shrinkThreshold Then         'limit upscaling

                Dim canvasTopDiff = (shrinkThreshold - skCanvas.Height) / 2
                Dim viewBoxDiff = canvasTopDiff * (sketchGrid.ActualHeight / shrinkThreshold)
                sketchViewbox.Margin = New Thickness(viewBoxDiff)

            Else

                'reduce the native margin around the sketch in CBDraw canvas
                sketchViewbox.Margin = New Thickness(0, -15, 0, 4)

            End If

        End If

    End Sub


    ''' <summary>
    ''' Gets the rectangle surrounding all component rectangles of the rection sketch in the canvas 
    ''' coordinate system.
    ''' </summary>
    ''' <returns>Overall sketch rectangle.</returns>
    ''' 
    Private Function GetOverallSketchRect() As Rect

        Dim overallRect = SketchInfo.Reactants.First.BoundingRect

        For Each react In SketchInfo.Reactants
            overallRect.Union(react.BoundingRect)
        Next

        For Each prod In SketchInfo.Products
            overallRect.Union(prod.BoundingRect)
        Next

        Return overallRect

    End Function


End Class
