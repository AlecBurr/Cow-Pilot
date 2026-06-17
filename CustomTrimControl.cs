using System.Globalization;
using System.Drawing.Drawing2D;

namespace CowPilot;

sealed class CustomTrimControl : Control
{
    private static readonly Color AxisColor = Color.FromArgb(120, 120, 120);
    private static readonly Color VertexColor = Color.White;
    private static readonly Color LabelTextColor = Color.White;
    private static readonly Color LabelBackgroundColor = Color.FromArgb(42, 42, 42);
    private static readonly Color[] BrightPalette =
    [
        Color.FromArgb(91, 166, 255),
        Color.FromArgb(255, 193, 7),
        Color.FromArgb(102, 187, 106),
        Color.FromArgb(236, 64, 122),
        Color.FromArgb(38, 198, 218),
        Color.FromArgb(255, 112, 67),
        Color.FromArgb(171, 71, 188),
        Color.FromArgb(212, 225, 87)
    ];
    private readonly List<CustomTrimPieceState> _pieces = [];
    private PointF? _drawingStart;
    private PointF? _previewPoint;
    private int _drawingPieceIndex = -1;
    private int _selectedPieceIndex = -1;
    private int _selectedVertexIndex = -1;
    private int _selectedSegmentIndex = -1;
    private int _selectedAngleVertexIndex = -1;
    private int _movingPieceIndex = -1;
    private int _rotatingPieceIndex = -1;
    private bool _pickingOrigin;
    private PointF? _lastMovePoint;
    private Point? _panStart;
    private float _panStartOffsetX;
    private float _panStartOffsetY;
    private PointF _rotationCenter;
    private double _rotationStartAngle;
    private List<PointF> _rotationBaseVertices = [];
    private PreferenceSettings _preferences = new();
    private PriceSettings _prices = new();

    public event EventHandler? TrimChanged;
    public event EventHandler? SelectionChanged;

    public float ZoomPixelsPerInch { get; private set; } = 64;
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public float SnapInches { get; set; } = 1f;
    public float RotationSnapDegrees { get; set; } = 15f;
    public int ColorSide { get; private set; } = 1;

    public CustomTrimControl()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(_preferences.CustomTrimBackgroundArgb);
        ForeColor = LabelTextColor;
        Dock = DockStyle.Fill;
        TabStop = true;
    }

    public int SelectedPieceIndex
    {
        get => _selectedPieceIndex;
        set => SelectPiece(value);
    }

    public int PieceCount => _pieces.Count;
    public IReadOnlyList<CustomTrimPieceState> Pieces => _pieces;
    public IReadOnlyList<PointF> SelectedVertices => SelectedPiece?.Vertices is { } vertices ? vertices : Array.Empty<PointF>();
    public int SelectedVertexIndex => _selectedVertexIndex;
    public int SelectedSegmentIndex => _selectedSegmentIndex;
    public int SelectedAngleVertexIndex => _selectedAngleVertexIndex;

    public int SelectedOriginIndex
    {
        get => SelectedPiece == null ? -1 : Math.Clamp(SelectedPiece.OriginIndex, 0, Math.Max(0, SelectedPiece.Vertices.Count - 1));
        set
        {
            if (SelectedPiece == null || SelectedPiece.Vertices.Count == 0) return;
            SelectedPiece.OriginIndex = Math.Clamp(value, 0, SelectedPiece.Vertices.Count - 1);
            Changed();
        }
    }

    public int SelectedQuantity
    {
        get => SelectedPiece?.Quantity ?? 1;
        set
        {
            if (SelectedPiece == null) return;
            int quantity = Math.Max(1, value);
            if (SelectedPiece.Quantity == quantity) return;
            SelectedPiece.Quantity = quantity;
            Changed();
        }
    }

    public string SelectedPieceText => SelectedPiece == null ? "Piece: none" : $"Piece: {_selectedPieceIndex + 1}/{_pieces.Count}";

    private CustomTrimPieceState? SelectedPiece => _selectedPieceIndex >= 0 && _selectedPieceIndex < _pieces.Count ? _pieces[_selectedPieceIndex] : null;

    public CustomTrimState State => new(_pieces.Where(p => p.Vertices.Count > 1).Select(CopyPiece).ToList(),
        ZoomPixelsPerInch, OffsetX, OffsetY, SnapInches, ColorSide);

    public void ApplySettings(AppSettings settings)
    {
        settings.Normalize();
        _preferences = settings.Preferences;
        _prices = settings.Prices;
        BackColor = Color.FromArgb(_preferences.CustomTrimBackgroundArgb);
        ForeColor = LabelTextColor;
        Invalidate();
    }

    public void LoadState(CustomTrimState state)
    {
        _pieces.Clear();
        _pieces.AddRange(state.Pieces.Select(CopyPiece));
        _selectedPieceIndex = _pieces.Count == 0 ? -1 : 0;
        ClearPartSelection();
        _drawingPieceIndex = _selectedPieceIndex;
        _drawingStart = null;
        _previewPoint = null;
        ZoomPixelsPerInch = Math.Clamp(state.ZoomPixelsPerInch <= 0 ? 64 : state.ZoomPixelsPerInch, 4, 360);
        OffsetX = state.OffsetX;
        OffsetY = state.OffsetY;
        SnapInches = state.SnapInches <= 0 ? 1f : state.SnapInches;
        ColorSide = state.ColorSide >= 0 ? 1 : -1;
        Changed();
    }

    public void BeginNewPiece()
    {
        var piece = new CustomTrimPieceState(1, []);
        _pieces.Add(piece);
        _selectedPieceIndex = _pieces.Count - 1;
        ClearPartSelection();
        _drawingPieceIndex = _selectedPieceIndex;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void SelectPiece(int index)
    {
        if (index < 0 || index >= _pieces.Count) return;
        _selectedPieceIndex = index;
        ClearPartSelection();
        _drawingPieceIndex = -1;
        _drawingStart = null;
        _previewPoint = null;
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectVertex(int index)
    {
        if (SelectedPiece == null || index < 0 || index >= SelectedPiece.Vertices.Count) return;
        _selectedVertexIndex = index;
        _selectedSegmentIndex = -1;
        _selectedAngleVertexIndex = IsInteriorVertex(_selectedPieceIndex, index) ? index : -1;
        ClearDrawing();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectSegment(int index)
    {
        if (SelectedPiece == null || index < 0 || index >= SelectedPiece.Vertices.Count - 1) return;
        _selectedVertexIndex = -1;
        _selectedSegmentIndex = index;
        _selectedAngleVertexIndex = -1;
        ClearDrawing();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SelectAngleVertex(int index)
    {
        if (!IsInteriorVertex(_selectedPieceIndex, index)) return;
        _selectedVertexIndex = index;
        _selectedSegmentIndex = -1;
        _selectedAngleVertexIndex = index;
        ClearDrawing();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool SetSelectedVertexAsOrigin()
    {
        if (SelectedPiece == null || _selectedVertexIndex < 0 || _selectedVertexIndex >= SelectedPiece.Vertices.Count) return false;
        SelectedOriginIndex = _selectedVertexIndex;
        return true;
    }

    public void AddVertex(PointF point)
    {
        if (SelectedPiece == null) BeginNewPiece();
        SelectedPiece!.Vertices.Add(point);
        SelectedPiece.OriginIndex = Math.Clamp(SelectedPiece.OriginIndex, 0, Math.Max(0, SelectedPiece.Vertices.Count - 1));
        _selectedVertexIndex = SelectedPiece.Vertices.Count - 1;
        _selectedSegmentIndex = -1;
        _selectedAngleVertexIndex = IsInteriorVertex(_selectedPieceIndex, _selectedVertexIndex) ? _selectedVertexIndex : -1;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void UpdateVertex(int index, PointF point)
    {
        if (SelectedPiece == null || index < 0 || index >= SelectedPiece.Vertices.Count) return;
        SelectedPiece.Vertices[index] = point;
        SelectedPiece.OriginIndex = Math.Clamp(SelectedPiece.OriginIndex, 0, Math.Max(0, SelectedPiece.Vertices.Count - 1));
        _selectedVertexIndex = index;
        _selectedSegmentIndex = -1;
        _selectedAngleVertexIndex = IsInteriorVertex(_selectedPieceIndex, index) ? index : -1;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void AddSegment(double length, double angleDegrees)
    {
        if (length <= 0) return;
        if (SelectedPiece == null) BeginNewPiece();
        if (SelectedPiece!.Vertices.Count == 0) SelectedPiece.Vertices.Add(new PointF(0, 0));
        PointF start = SelectedPiece.Vertices[^1];
        double radians = angleDegrees * Math.PI / 180.0;
        var end = new PointF((float)(start.X + Math.Cos(radians) * length), (float)(start.Y + Math.Sin(radians) * length));
        SelectedPiece.Vertices.Add(end);
        _selectedSegmentIndex = SelectedPiece.Vertices.Count - 2;
        _selectedVertexIndex = -1;
        _selectedAngleVertexIndex = -1;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void UpdateInteriorAngle(int vertexIndex, double interiorDegrees)
    {
        if (SelectedPiece == null) return;
        if (vertexIndex <= 0 || vertexIndex >= SelectedPiece.Vertices.Count - 1) return;
        PointF previous = SelectedPiece.Vertices[vertexIndex - 1];
        PointF vertex = SelectedPiece.Vertices[vertexIndex];
        PointF next = SelectedPiece.Vertices[vertexIndex + 1];
        double previousAngle = AngleRadians(vertex, previous);
        double nextAngle = AngleRadians(vertex, next);
        double targetNextAngle = previousAngle + NormalizeDegrees(interiorDegrees) * Math.PI / 180.0;
        double delta = NormalizeRadians(targetNextAngle - nextAngle);
        for (int i = vertexIndex + 1; i < SelectedPiece.Vertices.Count; i++)
        {
            SelectedPiece.Vertices[i] = RotatePoint(SelectedPiece.Vertices[i], vertex, delta);
        }
        _selectedVertexIndex = vertexIndex;
        _selectedSegmentIndex = -1;
        _selectedAngleVertexIndex = vertexIndex;
        ClearDrawing();
        Changed();
    }

    public void UpdateSegment(int segmentIndex, double length, double angleDegrees)
    {
        if (SelectedPiece == null || length <= 0) return;
        int endIndex = segmentIndex + 1;
        if (segmentIndex < 0 || endIndex >= SelectedPiece.Vertices.Count) return;
        PointF start = SelectedPiece.Vertices[segmentIndex];
        PointF oldEnd = SelectedPiece.Vertices[endIndex];
        double radians = angleDegrees * Math.PI / 180.0;
        PointF newEnd = new((float)(start.X + Math.Cos(radians) * length), (float)(start.Y + Math.Sin(radians) * length));
        float dx = newEnd.X - oldEnd.X;
        float dy = newEnd.Y - oldEnd.Y;
        for (int i = endIndex; i < SelectedPiece.Vertices.Count; i++)
        {
            PointF vertex = SelectedPiece.Vertices[i];
            SelectedPiece.Vertices[i] = new PointF(vertex.X + dx, vertex.Y + dy);
        }
        _selectedVertexIndex = -1;
        _selectedSegmentIndex = segmentIndex;
        _selectedAngleVertexIndex = -1;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void AddBendSegment(double length, double bendDegrees)
    {
        if (SelectedPiece == null || SelectedPiece.Vertices.Count < 2)
        {
            AddSegment(length, bendDegrees);
            return;
        }
        PointF previous = SelectedPiece.Vertices[^2];
        PointF current = SelectedPiece.Vertices[^1];
        double baseAngle = Math.Atan2(current.Y - previous.Y, current.X - previous.X) * 180.0 / Math.PI;
        AddSegment(length, baseAngle + bendDegrees);
    }

    public void AddSegmentFromPitch(double length, double rise, double run)
    {
        if (run == 0) throw new InvalidOperationException("Pitch run cannot be zero.");
        AddSegment(length, Math.Atan2(rise, run) * 180.0 / Math.PI);
    }

    public void Undo()
    {
        ClearDrawing();
        if (SelectedPiece == null) return;
        if (SelectedPiece.Vertices.Count > 0) SelectedPiece.Vertices.RemoveAt(SelectedPiece.Vertices.Count - 1);
        if (SelectedPiece.Vertices.Count == 0)
        {
            _pieces.RemoveAt(_selectedPieceIndex);
            _selectedPieceIndex = Math.Min(_selectedPieceIndex, _pieces.Count - 1);
        }
        TrimSelectionToBounds();
        _drawingPieceIndex = -1;
        Changed();
    }

    public void ClearPieces()
    {
        CancelOriginPick();
        _pieces.Clear();
        _selectedPieceIndex = -1;
        ClearPartSelection();
        _drawingPieceIndex = -1;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void Recenter()
    {
        ClearDrawing(removeDanglingVertex: true);
        CancelOriginPick();
        OffsetX = 0;
        OffsetY = 0;
        Changed();
    }

    public void Zoom(float multiplier)
    {
        ClearDrawing(removeDanglingVertex: true);
        CancelOriginPick();
        ZoomPixelsPerInch = Math.Clamp(ZoomPixelsPerInch * multiplier, 4, 360);
        Changed();
    }

    public void ToggleColorSide()
    {
        ClearDrawing(removeDanglingVertex: true);
        CancelOriginPick();
        ColorSide *= -1;
        Changed();
    }

    public void BeginOriginPick()
    {
        ClearDrawing();
        if (SelectedPiece == null) return;
        _pickingOrigin = true;
        Cursor = Cursors.Cross;
    }

    public void CancelDrawing()
    {
        ClearDrawing(removeDanglingVertex: true);
    }

    public string Summary()
    {
        double drawn = State.Pieces.Sum(QuoteCalculator.CustomTrimLength);
        double billed = State.Pieces.Sum(p => QuoteCalculator.CustomTrimLength(p) * p.Quantity);
        int bends = State.Pieces.Sum(p => QuoteCalculator.CustomTrimBends(p) * p.Quantity);
        double price = QuoteCalculator.CustomTrimPrice(State, _prices);
        return $"Drawn: {Num(drawn)}\"   Billed: {Num(billed)}\"   Bends: {bends}   Price: {Money(price)}";
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button != MouseButtons.Left)
        {
            bool cancelled = ClearDrawing(removeDanglingVertex: true);
            cancelled = CancelOriginPick() || cancelled;
            if (cancelled)
            {
                Changed();
                return;
            }
            if (e.Button == MouseButtons.Right && !cancelled)
            {
                _panStart = e.Location;
                _panStartOffsetX = OffsetX;
                _panStartOffsetY = OffsetY;
            }
            return;
        }
        if (_pickingOrigin)
        {
            PickOrigin(e.Location);
            return;
        }
        if (e.Button == MouseButtons.Left && _drawingStart == null)
        {
            if (TryStartRotation(e.Location)) return;
            var vertexHit = NearestVertex(e.Location);
            if (vertexHit != null)
            {
                SelectHitVertex(vertexHit.Value);
                return;
            }
            var segmentHit = NearestSegment(e.Location);
            if (segmentHit != null)
            {
                SelectHitSegment(segmentHit.Value);
                return;
            }
            int? pieceHit = HitPieceBounds(e.Location);
            if (pieceHit != null)
            {
                _selectedPieceIndex = pieceHit.Value;
                ClearPartSelection();
                _movingPieceIndex = pieceHit.Value;
                _lastMovePoint = ScreenToModel(e.Location);
                Invalidate();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        if (e.Button == MouseButtons.Left) HandleLeftClick(e.Location);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_panStart != null)
        {
            OffsetX = _panStartOffsetX + e.X - _panStart.Value.X;
            OffsetY = _panStartOffsetY + e.Y - _panStart.Value.Y;
            Invalidate();
            return;
        }
        if (_rotatingPieceIndex >= 0 && _rotatingPieceIndex < _pieces.Count)
        {
            RotateSelectedPiece(e.Location);
            Invalidate();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        if (_movingPieceIndex >= 0 && _lastMovePoint != null && _movingPieceIndex < _pieces.Count)
        {
            PointF current = ScreenToModel(e.Location);
            float dx = current.X - _lastMovePoint.Value.X;
            float dy = current.Y - _lastMovePoint.Value.Y;
            MovePiece(_pieces[_movingPieceIndex], dx, dy);
            _lastMovePoint = current;
            Invalidate();
            return;
        }
        if (_drawingStart != null)
        {
            _previewPoint = Snap(ScreenToModel(e.Location));
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Right) _panStart = null;
        if (e.Button == MouseButtons.Left && _rotatingPieceIndex >= 0)
        {
            _rotatingPieceIndex = -1;
            _rotationBaseVertices = [];
            Changed();
        }
        if (e.Button == MouseButtons.Left && _movingPieceIndex >= 0)
        {
            if (_movingPieceIndex < _pieces.Count) SnapPieceOrigin(_pieces[_movingPieceIndex]);
            _movingPieceIndex = -1;
            _lastMovePoint = null;
            Changed();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        ClearDrawing(removeDanglingVertex: true);
        Zoom(e.Delta > 0 ? 1.1f : 0.9f);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == Keys.Z) ClearDrawing(removeDanglingVertex: true);
        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        bool cancelled = ClearDrawing(removeDanglingVertex: true);
        cancelled = CancelOriginPick() || cancelled;
        if (cancelled)
        {
            Changed();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        DrawGrid(e.Graphics);
        DrawPieces(e.Graphics);
        DrawColorArrow(e.Graphics);
        DrawMaxLengthWarnings(e.Graphics);
    }

    private void HandleLeftClick(Point screen)
    {
        PointF snapped = Snap(ScreenToModel(screen));
        if (_drawingStart == null)
        {
            if (SelectedPiece == null || SelectedPiece.Vertices.Count > 1) BeginNewPiece();
            SelectedPiece!.Vertices.Add(snapped);
            _selectedVertexIndex = SelectedPiece.Vertices.Count - 1;
            _selectedSegmentIndex = -1;
            _selectedAngleVertexIndex = -1;
            _drawingPieceIndex = _selectedPieceIndex;
            _drawingStart = snapped;
            _previewPoint = snapped;
        }
        else
        {
            var piece = PieceForDrawing();
            if (Distance(_drawingStart.Value, snapped) < 0.0001f)
            {
                ClearDrawing(removeDanglingVertex: piece.Vertices.Count == 1);
            }
            else
            {
                if (piece.Vertices.Count == 0 || Distance(piece.Vertices[^1], _drawingStart.Value) > 0.0001f) piece.Vertices.Add(_drawingStart.Value);
                piece.Vertices.Add(snapped);
                _selectedPieceIndex = _drawingPieceIndex;
                _selectedVertexIndex = -1;
                _selectedSegmentIndex = piece.Vertices.Count - 2;
                _selectedAngleVertexIndex = -1;
                _drawingStart = snapped;
                _previewPoint = snapped;
            }
        }
        Changed();
    }

    private CustomTrimPieceState PieceForDrawing()
    {
        if (_drawingPieceIndex < 0 || _drawingPieceIndex >= _pieces.Count)
        {
            if (SelectedPiece == null) BeginNewPiece();
            _drawingPieceIndex = _selectedPieceIndex;
        }
        return _pieces[_drawingPieceIndex];
    }

    private VertexHit? NearestVertex(Point screen)
    {
        for (int pieceIndex = 0; pieceIndex < _pieces.Count; pieceIndex++)
        {
            for (int vertexIndex = 0; vertexIndex < _pieces[pieceIndex].Vertices.Count; vertexIndex++)
            {
                Point point = ModelToScreen(_pieces[pieceIndex].Vertices[vertexIndex]);
                if (Distance(point, screen) <= 9) return new VertexHit(pieceIndex, vertexIndex, _pieces[pieceIndex].Vertices[vertexIndex]);
            }
        }
        return null;
    }

    private SegmentHit? NearestSegment(Point screen)
    {
        for (int pieceIndex = _pieces.Count - 1; pieceIndex >= 0; pieceIndex--)
        {
            var vertices = _pieces[pieceIndex].Vertices;
            for (int segmentIndex = vertices.Count - 2; segmentIndex >= 0; segmentIndex--)
            {
                Point from = ModelToScreen(vertices[segmentIndex]);
                Point to = ModelToScreen(vertices[segmentIndex + 1]);
                if (DistanceToSegment(screen, from, to) <= 7) return new SegmentHit(pieceIndex, segmentIndex);
            }
        }
        return null;
    }

    private void SelectHitVertex(VertexHit hit)
    {
        _selectedPieceIndex = hit.PieceIndex;
        _drawingPieceIndex = -1;
        _selectedVertexIndex = hit.VertexIndex;
        _selectedSegmentIndex = -1;
        _selectedAngleVertexIndex = IsInteriorVertex(hit.PieceIndex, hit.VertexIndex) ? hit.VertexIndex : -1;
        ClearDrawing();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SelectHitSegment(SegmentHit hit)
    {
        _selectedPieceIndex = hit.PieceIndex;
        _drawingPieceIndex = -1;
        _selectedVertexIndex = -1;
        _selectedSegmentIndex = hit.SegmentIndex;
        _selectedAngleVertexIndex = -1;
        ClearDrawing();
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DrawGrid(Graphics g)
    {
        g.Clear(Color.FromArgb(_preferences.CustomTrimBackgroundArgb));
        if (!_preferences.ShowCustomTrimGrid) return;
        float minor = SnapInches * ZoomPixelsPerInch;
        float major = ZoomPixelsPerInch;
        Point center = CenterPoint();
        using var minorPen = new Pen(Color.FromArgb(_preferences.CustomTrimMinorGridArgb), _preferences.CustomTrimMinorGridThickness);
        using var majorPen = new Pen(Color.FromArgb(_preferences.CustomTrimMajorGridArgb), _preferences.CustomTrimMajorGridThickness);
        if (minor >= 6) DrawGridLines(g, minorPen, minor, center);
        if (major >= 6) DrawGridLines(g, majorPen, major, center);
        using var axisPen = new Pen(AxisColor, _preferences.CustomTrimMajorGridThickness);
        g.DrawLine(axisPen, 0, center.Y, Width, center.Y);
        g.DrawLine(axisPen, center.X, 0, center.X, Height);
    }

    private void DrawGridLines(Graphics g, Pen pen, float spacing, Point center)
    {
        for (float x = Mod(center.X, spacing); x < Width; x += spacing) g.DrawLine(pen, x, 0, x, Height);
        for (float y = Mod(center.Y, spacing); y < Height; y += spacing) g.DrawLine(pen, 0, y, Width, y);
    }

    private void DrawPieces(Graphics g)
    {
        for (int p = 0; p < _pieces.Count; p++)
        {
            var piece = _pieces[p];
            using var pen = PiecePen(p, p == _selectedPieceIndex);
            if (p == _selectedPieceIndex) DrawMarquee(g, piece);
            for (int i = 1; i < piece.Vertices.Count; i++) DrawSegment(g, pen, piece.Vertices[i - 1], piece.Vertices[i], true);
            if (_preferences.ShowCustomTrimAngleArcs) DrawAngleMarkers(g, piece, p);
            if (piece.Vertices.Count > 0) DrawQuantity(g, piece);
            for (int i = 0; i < piece.Vertices.Count; i++)
            {
                Point point = ModelToScreen(piece.Vertices[i]);
                bool selectedOrigin = p == _selectedPieceIndex && i == SelectedOriginIndex;
                bool selectedVertex = p == _selectedPieceIndex && i == _selectedVertexIndex;
                using var brush = new SolidBrush(selectedVertex ? Color.Red : selectedOrigin ? Color.FromArgb(_preferences.CustomTrimOriginArgb) : VertexColor);
                float size = selectedVertex ? _preferences.CustomTrimVertexSize + 4 : _preferences.CustomTrimVertexSize;
                g.FillEllipse(brush, point.X - size / 2f, point.Y - size / 2f, size, size);
            }
        }
        if (_drawingStart != null && _previewPoint != null)
        {
            using var preview = new Pen(Color.FromArgb(_preferences.CustomTrimSelectedLineArgb), Math.Max(1, _preferences.CustomTrimLineThickness - 1)) { DashStyle = DashStyle.Dash };
            DrawSegment(g, preview, _drawingStart.Value, _previewPoint.Value, true);
        }
    }

    private void DrawSegment(Graphics g, Pen pen, PointF from, PointF to, bool label)
    {
        Point a = ModelToScreen(from);
        Point b = ModelToScreen(to);
        g.DrawLine(pen, a, b);
        if (label) DrawLengthLabel(g, from, to);
    }

    private void DrawLengthLabel(Graphics g, PointF from, PointF to)
    {
        double length = Distance(from, to);
        if (length <= 0) return;
        string text = $"{Num(length)}\"";
        Point a = ModelToScreen(from);
        Point b = ModelToScreen(to);
        int x = (a.X + b.X) / 2;
        int y = (a.Y + b.Y) / 2;
        SizeF size = g.MeasureString(text, Font);
        using var background = new SolidBrush(LabelBackgroundColor);
        using var textBrush = new SolidBrush(LabelTextColor);
        g.FillRectangle(background, x - size.Width / 2 - 3, y - size.Height / 2 - 2, size.Width + 6, size.Height + 4);
        g.DrawString(text, Font, textBrush, x - size.Width / 2, y - size.Height / 2);
    }

    private void DrawQuantity(Graphics g, CustomTrimPieceState piece)
    {
        Point point = ModelToScreen(piece.Vertices[0]);
        using var textBrush = new SolidBrush(LabelTextColor);
        g.DrawString($"Qty {piece.Quantity}", Font, textBrush, point.X + 8, point.Y - 18);
    }

    private void DrawColorArrow(Graphics g)
    {
        var segment = LongestSegment(SelectedPiece) ?? _pieces.Select(LongestSegment).Where(s => s != null).MaxBy(s => s!.Value.Length);
        if (segment == null) return;
        PointF from = segment.Value.From;
        PointF to = segment.Value.To;
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        double length = Math.Sqrt(dx * dx + dy * dy);
        if (length <= 0) return;
        double nx = (-dy / length) * ColorSide;
        double ny = (dx / length) * ColorSide;
        var mid = new PointF((from.X + to.X) / 2, (from.Y + to.Y) / 2);
        Point start = ModelToScreen(new PointF((float)(mid.X + nx * 1.25), (float)(mid.Y + ny * 1.25)));
        Point end = ModelToScreen(new PointF((float)(mid.X + nx * 0.12), (float)(mid.Y + ny * 0.12)));
        Color color = Color.DeepSkyBlue;
        using var pen = new Pen(color, 4) { EndCap = LineCap.ArrowAnchor };
        g.DrawLine(pen, start, end);
        using var brush = new SolidBrush(color);
        g.DrawString("Color side", Font, brush, start.X + 6, start.Y - 6);
    }

    private void DrawMaxLengthWarnings(Graphics g)
    {
        List<string> warnings = [];
        for (int i = 0; i < _pieces.Count; i++)
        {
            double length = QuoteCalculator.CustomTrimLength(_pieces[i]);
            if (length > _prices.CustomTrimMaxInches)
            {
                warnings.Add($"Piece {i + 1}: {Num(length)}\" exceeds {Num(_prices.CustomTrimMaxInches)}\" max");
            }
        }
        if (warnings.Count == 0) return;
        string text = string.Join("   ", warnings);
        using var font = new Font(Font, FontStyle.Bold);
        SizeF size = g.MeasureString(text, font);
        float x = Math.Max(8, (Width - size.Width) / 2f);
        using var background = new SolidBrush(Color.FromArgb(180, 42, 42, 42));
        using var brush = new SolidBrush(Color.Red);
        g.FillRectangle(background, x - 6, 6, Math.Min(size.Width + 12, Width - 16), size.Height + 8);
        g.DrawString(text, font, brush, x, 10);
    }

    private Pen PiecePen(int pieceIndex, bool selected)
    {
        Color color = selected
            ? Color.FromArgb(_preferences.CustomTrimSelectedLineArgb)
            : PieceColor(pieceIndex);
        var pen = new Pen(color, selected ? _preferences.CustomTrimLineThickness + 0.5f : _preferences.CustomTrimLineThickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        return pen;
    }

    private Color PieceColor(int pieceIndex)
    {
        if (_preferences.UseRandomCustomTrimPieceColors) return BrightPalette[Math.Abs(pieceIndex) % BrightPalette.Length];
        return Color.FromArgb(_preferences.CustomTrimLineArgb);
    }

    private void DrawMarquee(Graphics g, CustomTrimPieceState piece)
    {
        if (!_preferences.ShowCustomTrimMarquee || piece.Vertices.Count == 0) return;
        Rectangle? bounds = ScreenBounds(piece);
        if (bounds == null) return;
        Rectangle rectangle = bounds.Value;
        rectangle.Inflate(12, 12);
        using var pen = new Pen(Color.FromArgb(_preferences.CustomTrimSelectedLineArgb), 1.5f) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(pen, rectangle);
        DrawRotationHandle(g, piece);
    }

    private void DrawRotationHandle(Graphics g, CustomTrimPieceState piece)
    {
        Rectangle? handle = RotationHandleBounds(piece);
        if (handle == null) return;
        Rectangle rectangle = handle.Value;
        using var fill = new SolidBrush(Color.FromArgb(35, 35, 35));
        using var border = new Pen(Color.DeepSkyBlue, 2);
        g.FillEllipse(fill, rectangle);
        g.DrawEllipse(border, rectangle);
        Rectangle arc = rectangle;
        arc.Inflate(-4, -4);
        g.DrawArc(border, arc, 35, 250);
        Point tip = new(rectangle.Right - 4, rectangle.Top + 7);
        g.DrawLine(border, tip, new Point(tip.X - 4, tip.Y - 4));
        g.DrawLine(border, tip, new Point(tip.X - 5, tip.Y + 2));
    }

    private void DrawAngleMarkers(Graphics g, CustomTrimPieceState piece, int pieceIndex)
    {
        for (int i = 1; i < piece.Vertices.Count - 1; i++)
        {
            Point vertex = ModelToScreen(piece.Vertices[i]);
            Point previous = ModelToScreen(piece.Vertices[i - 1]);
            Point next = ModelToScreen(piece.Vertices[i + 1]);
            double angle = BenderAngle(piece.Vertices[i - 1], piece.Vertices[i], piece.Vertices[i + 1]);
            float start = AngleDegrees(vertex, previous);
            float sweep = SweepDegrees(start, AngleDegrees(vertex, next));
            const float radius = 24;
            bool selected = pieceIndex == _selectedPieceIndex && i == _selectedAngleVertexIndex;
            using var pen = new Pen(selected ? Color.Red : Color.FromArgb(_preferences.CustomTrimSelectedLineArgb), selected ? 2.5f : 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawArc(pen, vertex.X - radius, vertex.Y - radius, radius * 2, radius * 2, start, sweep);
            string text = $"{Num(angle)} deg";
            using var brush = new SolidBrush(selected ? Color.Red : LabelTextColor);
            g.DrawString(text, Font, brush, vertex.X + 12, vertex.Y + 12);
        }
    }

    private int? HitPieceBounds(Point screen)
    {
        for (int i = _pieces.Count - 1; i >= 0; i--)
        {
            Rectangle? bounds = ScreenBounds(_pieces[i]);
            if (bounds == null) continue;
            Rectangle rectangle = bounds.Value;
            rectangle.Inflate(12, 12);
            if (rectangle.Contains(screen)) return i;
        }
        return null;
    }

    private bool TryStartRotation(Point screen)
    {
        if (SelectedPiece == null) return false;
        Rectangle? handle = RotationHandleBounds(SelectedPiece);
        if (handle == null || !handle.Value.Contains(screen)) return false;
        _rotatingPieceIndex = _selectedPieceIndex;
        _rotationCenter = PieceCenter(SelectedPiece);
        _rotationStartAngle = AngleRadians(_rotationCenter, ScreenToModel(screen));
        _rotationBaseVertices = SelectedPiece.Vertices.Select(p => new PointF(p.X, p.Y)).ToList();
        return true;
    }

    private void RotateSelectedPiece(Point screen)
    {
        if (_rotatingPieceIndex < 0 || _rotatingPieceIndex >= _pieces.Count || _rotationBaseVertices.Count == 0) return;
        double current = AngleRadians(_rotationCenter, ScreenToModel(screen));
        double delta = NormalizeRadians(current - _rotationStartAngle);
        if (RotationSnapDegrees > 0)
        {
            double snap = RotationSnapDegrees * Math.PI / 180.0;
            delta = Math.Round(delta / snap) * snap;
        }
        var piece = _pieces[_rotatingPieceIndex];
        for (int i = 0; i < piece.Vertices.Count && i < _rotationBaseVertices.Count; i++)
        {
            piece.Vertices[i] = RotatePoint(_rotationBaseVertices[i], _rotationCenter, delta);
        }
    }

    private void PickOrigin(Point screen)
    {
        var hit = NearestVertex(screen);
        if (hit != null)
        {
            _selectedPieceIndex = hit.Value.PieceIndex;
            _drawingPieceIndex = -1;
            _selectedVertexIndex = hit.Value.VertexIndex;
            _selectedSegmentIndex = -1;
            _selectedAngleVertexIndex = IsInteriorVertex(hit.Value.PieceIndex, hit.Value.VertexIndex) ? hit.Value.VertexIndex : -1;
            _pieces[_selectedPieceIndex].OriginIndex = hit.Value.VertexIndex;
            CancelOriginPick();
            Changed();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        CancelOriginPick();
        Invalidate();
    }

    private bool CancelOriginPick()
    {
        if (!_pickingOrigin) return false;
        _pickingOrigin = false;
        Cursor = Cursors.Default;
        return true;
    }

    private Rectangle? ScreenBounds(CustomTrimPieceState piece)
    {
        if (piece.Vertices.Count == 0) return null;
        var points = piece.Vertices.Select(ModelToScreen).ToList();
        int left = points.Min(p => p.X);
        int top = points.Min(p => p.Y);
        int right = points.Max(p => p.X);
        int bottom = points.Max(p => p.Y);
        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private Rectangle? RotationHandleBounds(CustomTrimPieceState piece)
    {
        Rectangle? bounds = ScreenBounds(piece);
        if (bounds == null) return null;
        Rectangle rectangle = bounds.Value;
        rectangle.Inflate(12, 12);
        return new Rectangle(rectangle.Right - 9, rectangle.Top - 32, 18, 18);
    }

    private PointF PieceCenter(CustomTrimPieceState piece)
    {
        if (piece.Vertices.Count == 0) return new PointF(0, 0);
        return new PointF((float)piece.Vertices.Average(p => p.X), (float)piece.Vertices.Average(p => p.Y));
    }

    private static void MovePiece(CustomTrimPieceState piece, float dx, float dy)
    {
        for (int i = 0; i < piece.Vertices.Count; i++)
        {
            PointF vertex = piece.Vertices[i];
            piece.Vertices[i] = new PointF(vertex.X + dx, vertex.Y + dy);
        }
    }

    private Segment? LongestSegment(CustomTrimPieceState? piece)
    {
        if (piece == null) return null;
        Segment? longest = null;
        for (int i = 1; i < piece.Vertices.Count; i++)
        {
            double length = Distance(piece.Vertices[i - 1], piece.Vertices[i]);
            if (longest == null || length > longest.Value.Length) longest = new Segment(piece.Vertices[i - 1], piece.Vertices[i], length);
        }
        return longest;
    }

    private Point CenterPoint() => new((int)Math.Round(Width / 2.0 + OffsetX), (int)Math.Round(Height / 2.0 + OffsetY));

    private Point ModelToScreen(PointF model)
    {
        Point center = CenterPoint();
        return new Point((int)Math.Round(center.X + model.X * ZoomPixelsPerInch), (int)Math.Round(center.Y - model.Y * ZoomPixelsPerInch));
    }

    private PointF ScreenToModel(Point point)
    {
        Point center = CenterPoint();
        return new PointF((point.X - center.X) / ZoomPixelsPerInch, (center.Y - point.Y) / ZoomPixelsPerInch);
    }

    private PointF Snap(PointF point)
    {
        return new PointF((float)(Math.Round(point.X / SnapInches) * SnapInches), (float)(Math.Round(point.Y / SnapInches) * SnapInches));
    }

    private void SnapPieceOrigin(CustomTrimPieceState piece)
    {
        if (piece.Vertices.Count == 0) return;
        int origin = Math.Clamp(piece.OriginIndex, 0, piece.Vertices.Count - 1);
        PointF current = piece.Vertices[origin];
        PointF snapped = Snap(current);
        MovePiece(piece, snapped.X - current.X, snapped.Y - current.Y);
    }

    private bool ClearDrawing(bool removeDanglingVertex = false)
    {
        if (_drawingStart == null && _previewPoint == null) return false;
        if (removeDanglingVertex && _drawingPieceIndex >= 0 && _drawingPieceIndex < _pieces.Count)
        {
            var piece = _pieces[_drawingPieceIndex];
            if (piece.Vertices.Count == 1 && Distance(piece.Vertices[0], _drawingStart ?? piece.Vertices[0]) < 0.0001f)
            {
                _pieces.RemoveAt(_drawingPieceIndex);
                _selectedPieceIndex = Math.Min(_selectedPieceIndex, _pieces.Count - 1);
            }
        }
        _drawingStart = null;
        _previewPoint = null;
        _drawingPieceIndex = -1;
        TrimSelectionToBounds();
        Invalidate();
        return true;
    }

    private void ClearPartSelection()
    {
        _selectedVertexIndex = -1;
        _selectedSegmentIndex = -1;
        _selectedAngleVertexIndex = -1;
    }

    private void TrimSelectionToBounds()
    {
        if (_selectedPieceIndex >= _pieces.Count) _selectedPieceIndex = _pieces.Count - 1;
        if (_selectedPieceIndex < 0)
        {
            ClearPartSelection();
            return;
        }
        var piece = _pieces[_selectedPieceIndex];
        if (_selectedVertexIndex >= piece.Vertices.Count) _selectedVertexIndex = -1;
        if (_selectedSegmentIndex >= piece.Vertices.Count - 1) _selectedSegmentIndex = -1;
        if (!IsInteriorVertex(_selectedPieceIndex, _selectedAngleVertexIndex)) _selectedAngleVertexIndex = -1;
    }

    private bool IsInteriorVertex(int pieceIndex, int vertexIndex)
        => pieceIndex >= 0 && pieceIndex < _pieces.Count && vertexIndex > 0 && vertexIndex < _pieces[pieceIndex].Vertices.Count - 1;

    private void Changed()
    {
        TrimSelectionToBounds();
        Invalidate();
        TrimChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CustomTrimPieceState CopyPiece(CustomTrimPieceState piece) => new(piece.Quantity, piece.Vertices.Select(p => new PointF(p.X, p.Y)).ToList())
    {
        OriginIndex = piece.OriginIndex
    };
    private static float Mod(float value, float modulus) => (value % modulus + modulus) % modulus;
    private static double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    private static double AngleRadians(PointF center, PointF point) => Math.Atan2(point.Y - center.Y, point.X - center.X);
    private static double NormalizeRadians(double angle)
    {
        while (angle > Math.PI) angle -= Math.PI * 2;
        while (angle < -Math.PI) angle += Math.PI * 2;
        return angle;
    }

    private static PointF RotatePoint(PointF point, PointF center, double radians)
    {
        double cos = Math.Cos(radians);
        double sin = Math.Sin(radians);
        double x = point.X - center.X;
        double y = point.Y - center.Y;
        return new PointF((float)(center.X + x * cos - y * sin), (float)(center.Y + x * sin + y * cos));
    }

    private static double DistanceToSegment(Point point, Point from, Point to)
    {
        double dx = to.X - from.X;
        double dy = to.Y - from.Y;
        if (dx == 0 && dy == 0) return Distance(point, from);
        double t = Math.Clamp(((point.X - from.X) * dx + (point.Y - from.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Distance(point, new Point((int)Math.Round(from.X + t * dx), (int)Math.Round(from.Y + t * dy)));
    }

    private static double BenderAngle(PointF previous, PointF vertex, PointF next)
    {
        double sweep = SignedInteriorSweep(previous, vertex, next);
        return Math.Abs(Math.Abs(sweep) - 180) < 0.001 ? 0 : 180 + sweep;
    }

    private static double SignedInteriorSweep(PointF previous, PointF vertex, PointF next)
    {
        double previousAngle = Math.Atan2(previous.Y - vertex.Y, previous.X - vertex.X) * 180.0 / Math.PI;
        double nextAngle = Math.Atan2(next.Y - vertex.Y, next.X - vertex.X) * 180.0 / Math.PI;
        return NormalizeDegrees(nextAngle - previousAngle);
    }

    private static double NormalizeDegrees(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }

    private static float AngleDegrees(Point center, Point point) => (float)(Math.Atan2(point.Y - center.Y, point.X - center.X) * 180.0 / Math.PI);

    private static float SweepDegrees(float start, float end)
    {
        float sweep = end - start;
        while (sweep > 180) sweep -= 360;
        while (sweep < -180) sweep += 360;
        return sweep;
    }

    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Money(double value) => value.ToString("C", CultureInfo.GetCultureInfo("en-US"));

    private readonly record struct VertexHit(int PieceIndex, int VertexIndex, PointF Vertex);
    private readonly record struct SegmentHit(int PieceIndex, int SegmentIndex);
    private readonly record struct Segment(PointF From, PointF To, double Length);
}
