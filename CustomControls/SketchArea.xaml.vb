Imports System.ComponentModel
Imports System.Windows
Imports ElnCoreModel
Imports System.Windows.Controls
Imports ElnBase.ELNCalculations
Imports ElnBase.ELNEnumerations
Imports System.Windows.Media
Imports ElnBase

Public Class SketchArea

    Public Event SketchEdited(sender As Object, IsReactantModified As Boolean)

    Public Shared Event SketchSourceChanged(sender As Object, sketchInfo As SketchResults)

    ''' <summary>
    ''' Sets or gets if the if the SketchSourceChangeEvent should fire.
    ''' </summary>
    '''
    Public Shared Property DisableSketchSourceChangedEvent As Boolean = False


    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

    End Sub


    Public Shared Shadows ReadOnly ReactionSketchProperty As DependencyProperty =
        DependencyProperty.Register("ReactionSketch", GetType(String), GetType(SketchArea),
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

        Dim skArea = DirectCast(o, SketchArea)
        Dim currExp = CType(skArea.DataContext, tblExperiments)

        With skArea

            .SketchInfo = DrawingEditor.GetSketchInfo(e.NewValue)
            If .SketchInfo IsNot Nothing Then
                .DrawReactionSketch()
                .blkClickInfo.Visibility = Visibility.Collapsed
            Else
                CType(.sketchViewbox.Child, Canvas).Children.Clear()
                .blkClickInfo.Visibility = Visibility.Visible
            End If

            If Not DisableSketchSourceChangedEvent Then
                .RaiseSketchSourceChangedEvent()
            End If

        End With

    End Sub


    ''' <summary>
    ''' Raises the SketchSourceChanged event.
    ''' </summary>
    ''' 
    Private Sub RaiseSketchSourceChangedEvent()
        RaiseEvent SketchSourceChanged(Me, SketchInfo)
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

        Dim currExp = CType(Me.DataContext, tblExperiments)
        EditSketch(currExp)

    End Sub


    ''' <summary>
    ''' Displays the sketch editor after a mouse click.
    ''' </summary>
    ''' 
    Public Sub EditSketch(currExp As tblExperiments)

        Dim cbDraw As New DrawingEditor(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\Phoenix ELN Data")

        cbDraw.DialogProperties.SketchValidation = EditorOptions.SketchConditions.Reaction

        'get editor preferences from settings
        With cbDraw.DialogProperties
            .DialogPosition = New Point(My.Settings.CbDrawDialogPosition.X, My.Settings.CbDrawDialogPosition.Y)
            .DialogSize = New Size(My.Settings.CbDrawDialogSize.Width, My.Settings.CbDrawDialogSize.Height)
            .LastOpenFilePath = My.Settings.CbDrawLastOpenPath
            .LastSaveFilePath = My.Settings.CbDrawLastSavePath
            If Me.DataContext IsNot Nothing Then
                .IsOkButtonDisabled = CType(Me.DataContext, tblExperiments).WorkflowState = WorkflowStatus.Finalized
            End If
        End With

        'display sketch editor
        Dim skInfo = cbDraw.DisplayDialog(currExp.RxnSketch)

        'save editor preferences to settings
        With My.Settings
            .CbDrawDialogPosition = New System.Drawing.Point(cbDraw.DialogProperties.DialogPosition.X, cbDraw.DialogProperties.DialogPosition.Y)
            .CbDrawDialogSize = New System.Drawing.Size(cbDraw.DialogProperties.DialogSize.Width, cbDraw.DialogProperties.DialogSize.Height)
            .CbDrawLastOpenPath = cbDraw.DialogProperties.LastOpenFilePath
            .CbDrawLastSavePath = cbDraw.DialogProperties.LastSaveFilePath
        End With

        'editor not cancelled?
        If skInfo IsNot Nothing Then

            Dim reactantModified As Boolean = False

            'reactant molweight modified?
            If SketchInfo Is Nothing OrElse (skInfo.Reactants.First.Molweight <> SketchInfo.Reactants.First.Molweight) Then
                UpdateRefReactantProperties(currExp, skInfo)
                reactantModified = True
            End If

            'update product properties
            For i = 0 To skInfo.Products.Count - 1
                UpdateProductProperties(currExp, skInfo, i)   'skips if prodIndex does not exist
            Next

            SketchInfo = skInfo

            'update sketch related experiment data
            With currExp
                .RxnSketch = SketchInfo.NativeReactionXML
                .MDLRxnFileString = SketchInfo.MDLRxnFileString
                .ReactantInChIKey = SketchInfo.Reactants.First.InChIKey
                .ProductInChIKey = SketchInfo.Products.First.InChIKey
            End With

            'register RSS query fields
            Dim rss As New RxnSubstructure

            If Not rss.RegisterReactionRSS(currExp) Then

                MsgBox("Your reaction sketch seems to have a structure" + vbCrLf +
                       "error (stereochemistry?) and therefore will not" + vbCrLf +
                       "not be searchable! - Please try to correct if " + vbCrLf +
                       "before continuing ... ",
                       MsgBoxStyle.OkOnly + MsgBoxStyle.Information, "Sketch Validation")
            End If

            blkClickInfo.Visibility = Visibility.Collapsed
            RaiseEvent SketchEdited(Me, reactantModified)

        End If

    End Sub


    ''' <summary>
    ''' Populates the ReferenceReactant entry with sketch data.
    ''' </summary>
    ''' 
    Private Sub UpdateRefReactantProperties(currExp As tblExperiments, skInfo As SketchResults)

        For Each protItem In currExp.tblProtocolItems
            If protItem.ElementType = ProtocolElementType.RefReactant Then
                Dim reactEntry = protItem.tblRefReactants
                With skInfo.Reactants.First
                    reactEntry.MolecularWeight = .Molweight
                    reactEntry.MMols = 1000 * reactEntry.Grams / .Molweight
                    reactEntry.InChIKey = .InChIKey
                End With
            End If
        Next

        currExp.RefReactantMMols = GetTotalRefReactantMmols(currExp)

    End Sub


    ''' <summary>
    ''' Populates the ReferenceProduct entry with sketch data.
    ''' </summary>
    ''' <param name="prodIndex">The index of the product structure in left to right sequence in the sketch.</param>
    ''' 
    Private Sub UpdateProductProperties(currExp As tblExperiments, skInfo As SketchResults, prodIndex As Integer)

        Dim products = From prot In currExp.tblProtocolItems Where prot.ElementType = ProtocolElementType.Product AndAlso
            prot.tblProducts IsNot Nothing AndAlso prot.tblProducts.ProductIndex = prodIndex Select prot.tblProducts

        With skInfo.Products(prodIndex)
            For Each prod In products
                prod.MolecularWeight = .Molweight
                prod.ExactMass = .ExactMass
                prod.ElementalFormula = .EFString
                prod.InChIKey = .InChIKey
            Next
        End With

        If prodIndex = 0 Then
            Dim refYield = GetTotalProductYield(currExp, 0)
            currExp.Yield = If(refYield = 0, Nothing, refYield)
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

            Dim shrinkThreshold = 700

            If skCanvas.Height < shrinkThreshold Then         'limit upscaling

                Dim canvasTopDiff = (shrinkThreshold - skCanvas.Height) / 2
                Dim viewBoxDiff = canvasTopDiff * (sketchGrid.Height / shrinkThreshold)
                sketchViewbox.Margin = New Thickness(viewBoxDiff)

            Else

                'reduce the native margin around the sketch in CBDraw canvas
                sketchViewbox.Margin = New Thickness(0, -15, 0, 4)

            End If

            SetComponentLabels()

        End If

    End Sub


    ''' <summary>
    ''' Gets the currently calculated component label font size (used or printing)
    ''' </summary>
    '''
    Public Property ComponentFontSize As Double? = Nothing


    ''' <summary>
    ''' Gets the currently calculated component label bottom offset
    ''' </summary>
    '''
    Public Property BottomOffset As Double? = Nothing


    ''' <summary>
    ''' Places labels A, B, C ... below components.
    ''' </summary>
    ''' 
    Public Sub SetComponentLabels(Optional fontSize As Double? = Nothing, Optional bOffset As Double? = Nothing)

        Dim currExp = CType(DataContext, tblExperiments)

        'finalize internal canvas layout
        With CType(sketchViewbox.Child, Canvas)
            .Measure(New Size)
            .Arrange(New Rect)
        End With
        sketchViewbox.UpdateLayout()

        If fontSize Is Nothing AndAlso bOffset Is Nothing Then

            'adjust element sizes to ViewBox scaleFactor (ViewBox must be in visual tree!)
            Dim child As ContainerVisual = VisualTreeHelper.GetChild(sketchViewbox, 0)
            Dim scaleTransf As ScaleTransform = child.Transform
            Dim scaleFactor As Double = If(Not IsNothing(scaleTransf), scaleTransf.ScaleX, 1)
            ComponentFontSize = 12 / scaleFactor
            BottomOffset = 2 / scaleFactor

        Else

            ComponentFontSize = fontSize
            BottomOffset = bOffset

        End If

        'remove current index tags
        Dim myCanvas As Canvas = CType(sketchViewbox.Child, Canvas)
        For i = myCanvas.Children.Count - 1 To 0 Step -1
            Dim myItem As Object = myCanvas.Children(i)
            If Not IsNothing(myItem.tag) AndAlso myItem.tag = "cpdIndex" Then
                myCanvas.Children.Remove(myItem)
            End If
        Next

        'products:
        Dim prodIndex As Integer = 0
        For Each prod In SketchInfo.Products

            Dim prodRect = prod.BoundingRect
            Dim blkLabel = New TextBlock

            With blkLabel

                .Tag = "cpdIndex"
                .FontFamily = New FontFamily("Calibri")
                .FontSize = ComponentFontSize
                .FontWeight = FontWeights.DemiBold
                .TextAlignment = TextAlignment.Center
                .Foreground = Brushes.Blue
                .Opacity = 0.85
                .Text = Chr(Asc("A") + prodIndex)

                Dim yield = GetTotalProductYield(currExp, prodIndex)
                If yield IsNot Nothing AndAlso yield > 0 Then

                    'adapt yield display precision to actual value 
                    If yield > 10 Then
                        .Text += ": " + SignificantDigitsString(yield, 3) + "%"
                    Else
                        .Text += ": " + SignificantDigitsString(yield, 2) + "%" 'yields < 1%: diminished sigDigs
                    End If

                    'allow full labels for small bounding boxes with yield
                    If prodRect.Width > 250 Then
                        .Width = prodRect.Width
                    Else
                        .TextAlignment = TextAlignment.Left
                    End If

                Else
                    .Width = prodRect.Width
                End If

                Dim lowestBottom As Double = GetOverallSketchRect().Bottom + sketchViewbox.Margin.Bottom - 15
                Canvas.SetLeft(blkLabel, prodRect.Left)
                Canvas.SetTop(blkLabel, BottomOffset + lowestBottom)

                myCanvas.Children.Add(blkLabel)

            End With

            prodIndex += 1

        Next

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
