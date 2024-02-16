Imports System.ComponentModel
Imports System.Windows
Imports ElnCoreModel
Imports System.Windows.Controls
Imports ElnBase.ELNCalculations
Imports ElnBase.ELNEnumerations
Imports System.Windows.Media

Public Class SketchArea

    Public Event ReactionSketchChanged(sender As Object, IsReactantModified As Boolean)

    Public Shared Event SketchInfoAvailable(sender As Object, sketchInfo As SketchResults)


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

            .RaiseSketchInfoAvailableEvent()

        End With

    End Sub


    ''' <summary>
    ''' Raises the ReactionSketchChanged event.
    ''' </summary>
    ''' 
    Private Sub RaiseSketchInfoAvailableEvent()
        RaiseEvent SketchInfoAvailable(Me, SketchInfo)
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

        If Me.DataContext IsNot Nothing Then
            cbDraw.DialogProperties.IsOkButtonDisabled = CType(Me.DataContext, tblExperiments).WorkflowState = WorkflowStatus.Finalized
        End If

        Dim skInfo = cbDraw.DisplayDialog(currExp.RxnSketch)

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

            blkClickInfo.Visibility = Visibility.Collapsed

            RaiseEvent ReactionSketchChanged(Me, reactantModified)

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
    ''' Places labels A, B, C ... below components.
    ''' </summary>
    ''' 
    Public Sub SetComponentLabels()

        Dim bottomOffset As Single
        Dim cpdFontSize As Double
        Dim currExp = CType(DataContext, tblExperiments)

        'finalize internal canvas layout
        With CType(sketchViewbox.Child, Canvas)
            .Measure(New Size)
            .Arrange(New Rect)
        End With
        sketchViewbox.UpdateLayout()

        'adjust element sizes to ViewBox scaleFactor (ViewBox must be in visual tree!)
        Dim child As ContainerVisual = VisualTreeHelper.GetChild(sketchViewbox, 0)
        Dim scaleTransf As ScaleTransform = child.Transform
        Dim scaleFactor As Double = If(Not IsNothing(scaleTransf), scaleTransf.ScaleX, 1)
        cpdFontSize = 10.5 / scaleFactor
        bottomOffset = 2 / scaleFactor

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
                .FontFamily = New FontFamily("Comic Sans")
                .FontSize = cpdFontSize
                .FontWeight = FontWeights.DemiBold
                .TextAlignment = TextAlignment.Center
                .Foreground = Brushes.Blue
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
                Canvas.SetTop(blkLabel, bottomOffset + lowestBottom)

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
