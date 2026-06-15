using System.Globalization;
using System.Drawing.Drawing2D;

namespace CowPilot;

sealed class CustomTrimControl : Control
{
    private readonly List<CustomTrimPieceState> _pieces = [];
    private readonly Random _random = new();
    private PointF? _drawingStart;
    private PointF? _previewPoint;
    private int _drawingPieceIndex = -1;
    private int _selectedPieceIndex = -1;
    private int _movingPieceIndex = -1;
    private PointF? _lastMovePoint;
    private Point? _panStart;
    private float _panStartOffsetX;
    private float _panStartOffsetY;
    private PreferenceSettings _preferences = new();
    private PriceSettings _prices = new();

    public event EventHandler? TrimChanged;
    public event EventHandler? SelectionChanged;

    public float ZoomPixelsPerInch { get; private set; } = 64;
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public float SnapInches { get; set; } = 0.125f;
    public int ColorSide { get; private set; } = 1;

    public CustomTrimControl()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(_preferences.CustomTrimBackgroundArgb);
        ForeColor = Color.FromArgb(_preferences.CustomTrimLabelTextArgb);
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
        ForeColor = Color.FromArgb(_preferences.CustomTrimLabelTextArgb);
        foreach (var piece in _pieces) EnsurePieceColor(piece);
        Invalidate();
    }

    public void LoadState(CustomTrimState state)
    {
        _pieces.Clear();
        _pieces.AddRange(state.Pieces.Select(CopyPiece));
        foreach (var piece in _pieces) EnsurePieceColor(piece);
        _selectedPieceIndex = _pieces.Count == 0 ? -1 : 0;
        _drawingPieceIndex = _selectedPieceIndex;
        _drawingStart = null;
        _previewPoint = null;
        ZoomPixelsPerInch = Math.Clamp(state.ZoomPixelsPerInch <= 0 ? 64 : state.ZoomPixelsPerInch, 4, 360);
        OffsetX = state.OffsetX;
        OffsetY = state.OffsetY;
        SnapInches = state.SnapInches <= 0 ? 0.125f : state.SnapInches;
        ColorSide = state.ColorSide >= 0 ? 1 : -1;
        Changed();
    }

    public void BeginNewPiece()
    {
        var piece = new CustomTrimPieceState(1, []);
        EnsurePieceColor(piece);
        _pieces.Add(piece);
        _selectedPieceIndex = _pieces.Count - 1;
        _drawingPieceIndex = _selectedPieceIndex;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void SelectPiece(int index)
    {
        if (index < 0 || index >= _pieces.Count) return;
        _selectedPieceIndex = index;
        _drawingPieceIndex = index;
        _drawingStart = null;
        _previewPoint = null;
        Invalidate();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    public void AddVertex(PointF point)
    {
        if (SelectedPiece == null) BeginNewPiece();
        SelectedPiece!.Vertices.Add(point);
        SelectedPiece.OriginIndex = Math.Clamp(SelectedPiece.OriginIndex, 0, Math.Max(0, SelectedPiece.Vertices.Count - 1));
        _drawingStart = point;
        _drawingPieceIndex = _selectedPieceIndex;
        Changed();
    }

    public void UpdateVertex(int index, PointF point)
    {
        if (SelectedPiece == null || index < 0 || index >= SelectedPiece.Vertices.Count) return;
        SelectedPiece.Vertices[index] = point;
        SelectedPiece.OriginIndex = Math.Clamp(SelectedPiece.OriginIndex, 0, Math.Max(0, SelectedPiece.Vertices.Count - 1));
        _drawingStart = SelectedPiece.Vertices.LastOrDefault();
        _drawingPieceIndex = _selectedPieceIndex;
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
        SelectedPiece.Vertices.Add(Snap(end));
        _drawingStart = SelectedPiece.Vertices[^1];
        _drawingPieceIndex = _selectedPieceIndex;
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
        if (SelectedPiece == null) return;
        if (SelectedPiece.Vertices.Count > 0) SelectedPiece.Vertices.RemoveAt(SelectedPiece.Vertices.Count - 1);
        if (SelectedPiece.Vertices.Count == 0)
        {
            _pieces.RemoveAt(_selectedPieceIndex);
            _selectedPieceIndex = Math.Min(_selectedPieceIndex, _pieces.Count - 1);
        }
        _drawingStart = SelectedPiece?.Vertices.LastOrDefault();
        _drawingPieceIndex = _selectedPieceIndex;
        Changed();
    }

    public void ClearPieces()
    {
        _pieces.Clear();
        _selectedPieceIndex = -1;
        _drawingPieceIndex = -1;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
    }

    public void Recenter()
    {
        OffsetX = 0;
        OffsetY = 0;
        Changed();
    }

    public void Zoom(float multiplier)
    {
        ZoomPixelsPerInch = Math.Clamp(ZoomPixelsPerInch * multiplier, 4, 360);
        Changed();
    }

    public void ToggleColorSide()
    {
        ColorSide *= -1;
        Changed();
    }

    public string Summary()
    {
        double drawn = State.Pieces.Sum(QuoteCalculator.CustomTrimLength);
        double billed = State.Pieces.Sum(p => QuoteCalculator.CustomTrimLength(p) * p.Quantity);
        int bends = State.Pieces.Sum(p => QuoteCalculator.CustomTrimBends(p) * p.Quantity);
        double price = QuoteCalculator.CustomTrimPrice(State, _prices);
        string warning = drawn > _prices.CustomTrimMaxInches ? $"   WARNING: above {Num(_prices.CustomTrimMaxInches)}\" max" : "";
        return $"Drawn: {Num(drawn)}\"   Billed: {Num(billed)}\"   Bends: {bends}   Price: {Money(price)}{warning}";
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        if (e.Button == MouseButtons.Right)
        {
            _panStart = e.Location;
            _panStartOffsetX = OffsetX;
            _panStartOffsetY = OffsetY;
            return;
        }
        if (e.Button == MouseButtons.Left && _drawingStart == null)
        {
            int? pieceHit = HitPieceBounds(e.Location);
            if (pieceHit != null && NearestVertex(e.Location) == null)
            {
                _selectedPieceIndex = pieceHit.Value;
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
        if (e.Button == MouseButtons.Left && _movingPieceIndex >= 0)
        {
            _movingPieceIndex = -1;
            _lastMovePoint = null;
            Changed();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        Zoom(e.Delta > 0 ? 1.1f : 0.9f);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Control && e.KeyCode == Keys.Z)
        {
            Undo();
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
    }

    private void HandleLeftClick(Point screen)
    {
        PointF snapped = Snap(ScreenToModel(screen));
        if (_drawingStart == null)
        {
            var hit = NearestVertex(screen);
            if (hit != null)
            {
                _selectedPieceIndex = hit.Value.PieceIndex;
                _drawingPieceIndex = hit.Value.VertexIndex == _pieces[hit.Value.PieceIndex].Vertices.Count - 1
                    ? hit.Value.PieceIndex
                    : CreatePieceFrom(hit.Value.Vertex);
                _drawingStart = hit.Value.Vertex;
                _previewPoint = snapped;
            }
            else
            {
                if (SelectedPiece == null || SelectedPiece.Vertices.Count > 1) BeginNewPiece();
                SelectedPiece!.Vertices.Add(snapped);
                _drawingPieceIndex = _selectedPieceIndex;
                _drawingStart = snapped;
                _previewPoint = snapped;
            }
        }
        else
        {
            var piece = PieceForDrawing();
            if (Distance(_drawingStart.Value, snapped) < 0.0001f)
            {
                _drawingStart = null;
                _previewPoint = null;
            }
            else
            {
                if (piece.Vertices.Count == 0 || Distance(piece.Vertices[^1], _drawingStart.Value) > 0.0001f) piece.Vertices.Add(_drawingStart.Value);
                piece.Vertices.Add(snapped);
                _drawingStart = snapped;
                _previewPoint = snapped;
            }
        }
        Changed();
    }

    private int CreatePieceFrom(PointF point)
    {
        var piece = new CustomTrimPieceState(SelectedPiece?.Quantity ?? 1, [point]);
        EnsurePieceColor(piece);
        _pieces.Add(piece);
        _selectedPieceIndex = _pieces.Count - 1;
        return _selectedPieceIndex;
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
        using var axisPen = new Pen(Color.FromArgb(_preferences.CustomTrimAxisArgb), _preferences.CustomTrimMajorGridThickness);
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
            EnsurePieceColor(piece);
            using var pen = PiecePen(piece, p == _selectedPieceIndex);
            if (p == _selectedPieceIndex) DrawMarquee(g, piece);
            for (int i = 1; i < piece.Vertices.Count; i++) DrawSegment(g, pen, piece.Vertices[i - 1], piece.Vertices[i], true);
            if (_preferences.ShowCustomTrimAngleArcs) DrawAngleMarkers(g, piece);
            if (piece.Vertices.Count > 0) DrawQuantity(g, piece);
            for (int i = 0; i < piece.Vertices.Count; i++)
            {
                Point point = ModelToScreen(piece.Vertices[i]);
                bool selectedOrigin = p == _selectedPieceIndex && i == SelectedOriginIndex;
                using var brush = new SolidBrush(selectedOrigin ? Color.FromArgb(_preferences.CustomTrimOriginArgb) : Color.FromArgb(_preferences.CustomTrimVertexArgb));
                float size = _preferences.CustomTrimVertexSize;
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
        using var background = new SolidBrush(Color.FromArgb(_preferences.CustomTrimLabelBackgroundArgb));
        using var textBrush = new SolidBrush(Color.FromArgb(_preferences.CustomTrimLabelTextArgb));
        g.FillRectangle(background, x - size.Width / 2 - 3, y - size.Height / 2 - 2, size.Width + 6, size.Height + 4);
        g.DrawString(text, Font, textBrush, x - size.Width / 2, y - size.Height / 2);
    }

    private void DrawQuantity(Graphics g, CustomTrimPieceState piece)
    {
        Point point = ModelToScreen(piece.Vertices[0]);
        using var textBrush = new SolidBrush(Color.FromArgb(_preferences.CustomTrimLabelTextArgb));
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
        Point start = ModelToScreen(new PointF((float)(mid.X + nx * 0.4), (float)(mid.Y + ny * 0.4)));
        Point end = ModelToScreen(new PointF((float)(mid.X + nx * 1.1), (float)(mid.Y + ny * 1.1)));
        Color color = Color.FromArgb(_preferences.CustomTrimSelectedLineArgb);
        using var pen = new Pen(color, 2);
        g.DrawLine(pen, start, end);
        using var brush = new SolidBrush(color);
        g.DrawString("Color side", Font, brush, end.X + 6, end.Y - 6);
    }

    private Pen PiecePen(CustomTrimPieceState piece, bool selected)
    {
        Color color = selected
            ? Color.FromArgb(_preferences.CustomTrimSelectedLineArgb)
            : PieceColor(piece);
        var pen = new Pen(color, selected ? _preferences.CustomTrimSelectedLineThickness : _preferences.CustomTrimLineThickness)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        return pen;
    }

    private Color PieceColor(CustomTrimPieceState piece)
    {
        if (_preferences.UseRandomCustomTrimPieceColors && piece.ColorArgb is int argb) return Color.FromArgb(argb);
        return Color.FromArgb(_preferences.CustomTrimLineArgb);
    }

    private void EnsurePieceColor(CustomTrimPieceState piece)
    {
        if (!_preferences.UseRandomCustomTrimPieceColors)
        {
            piece.ColorArgb = null;
            return;
        }
        if (piece.ColorArgb != null) return;
        piece.ColorArgb = RandomBrightColor().ToArgb();
    }

    private Color RandomBrightColor()
    {
        Color color;
        do
        {
            color = Color.FromArgb(_random.Next(85, 256), _random.Next(85, 256), _random.Next(85, 256));
        }
        while (color.GetBrightness() < 0.48f);
        return color;
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
    }

    private void DrawAngleMarkers(Graphics g, CustomTrimPieceState piece)
    {
        for (int i = 1; i < piece.Vertices.Count - 1; i++)
        {
            Point vertex = ModelToScreen(piece.Vertices[i]);
            Point previous = ModelToScreen(piece.Vertices[i - 1]);
            Point next = ModelToScreen(piece.Vertices[i + 1]);
            double angle = InteriorAngle(piece.Vertices[i - 1], piece.Vertices[i], piece.Vertices[i + 1]);
            float start = AngleDegrees(vertex, previous);
            float sweep = SweepDegrees(start, AngleDegrees(vertex, next));
            const float radius = 24;
            using var pen = new Pen(Color.FromArgb(_preferences.CustomTrimSelectedLineArgb), 1.5f) { DashStyle = DashStyle.Dot };
            g.DrawArc(pen, vertex.X - radius, vertex.Y - radius, radius * 2, radius * 2, start, sweep);
            string text = $"{Num(angle)}°";
            using var brush = new SolidBrush(Color.FromArgb(_preferences.CustomTrimLabelTextArgb));
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

    private void Changed()
    {
        Invalidate();
        TrimChanged?.Invoke(this, EventArgs.Empty);
    }

    private static CustomTrimPieceState CopyPiece(CustomTrimPieceState piece) => new(piece.Quantity, piece.Vertices.Select(p => new PointF(p.X, p.Y)).ToList())
    {
        ColorArgb = piece.ColorArgb,
        OriginIndex = piece.OriginIndex
    };
    private static float Mod(float value, float modulus) => (value % modulus + modulus) % modulus;
    private static double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    private static double InteriorAngle(PointF previous, PointF vertex, PointF next)
    {
        double ax = previous.X - vertex.X;
        double ay = previous.Y - vertex.Y;
        double bx = next.X - vertex.X;
        double by = next.Y - vertex.Y;
        double lengths = Math.Sqrt(ax * ax + ay * ay) * Math.Sqrt(bx * bx + by * by);
        if (lengths <= 0) return 0;
        return Math.Acos(Math.Clamp(((ax * bx) + (ay * by)) / lengths, -1, 1)) * 180.0 / Math.PI;
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
    private readonly record struct Segment(PointF From, PointF To, double Length);
}
