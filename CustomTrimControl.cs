using System.Globalization;

namespace CowPilot;

sealed class CustomTrimControl : Control
{
    private readonly List<CustomTrimPieceState> _pieces = [];
    private PointF? _drawingStart;
    private PointF? _previewPoint;
    private int _drawingPieceIndex = -1;
    private int _selectedPieceIndex = -1;
    private Point? _panStart;
    private float _panStartOffsetX;
    private float _panStartOffsetY;

    public event EventHandler? TrimChanged;

    public float ZoomPixelsPerInch { get; private set; } = 64;
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }
    public float SnapInches { get; set; } = 0.125f;
    public int ColorSide { get; private set; } = 1;

    public CustomTrimControl()
    {
        DoubleBuffered = true;
        BackColor = Color.White;
        ForeColor = Color.Black;
        Dock = DockStyle.Fill;
        TabStop = true;
    }

    public int SelectedQuantity
    {
        get => SelectedPiece?.Quantity ?? 1;
        set
        {
            if (SelectedPiece == null) return;
            SelectedPiece.Quantity = Math.Max(1, value);
            Changed();
        }
    }

    public string SelectedPieceText => SelectedPiece == null ? "Piece: none" : $"Piece: {_selectedPieceIndex + 1}/{_pieces.Count}";

    private CustomTrimPieceState? SelectedPiece => _selectedPieceIndex >= 0 && _selectedPieceIndex < _pieces.Count ? _pieces[_selectedPieceIndex] : null;

    public CustomTrimState State => new(_pieces.Where(p => p.Vertices.Count > 1).Select(CopyPiece).ToList(),
        ZoomPixelsPerInch, OffsetX, OffsetY, SnapInches, ColorSide);

    public void LoadState(CustomTrimState state)
    {
        _pieces.Clear();
        _pieces.AddRange(state.Pieces.Select(CopyPiece));
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
        _pieces.Add(new CustomTrimPieceState(1, []));
        _selectedPieceIndex = _pieces.Count - 1;
        _drawingPieceIndex = _selectedPieceIndex;
        _drawingStart = null;
        _previewPoint = null;
        Changed();
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
        double price = QuoteCalculator.CustomTrimPrice(State);
        return $"Drawn: {Num(drawn)}\"   Billed: {Num(billed)}\"   Bends: {bends}   Price: {Money(price)}";
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
        _pieces.Add(new CustomTrimPieceState(SelectedPiece?.Quantity ?? 1, [point]));
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
        float minor = SnapInches * ZoomPixelsPerInch;
        float major = ZoomPixelsPerInch;
        Point center = CenterPoint();
        using var minorPen = new Pen(Color.Gainsboro);
        using var majorPen = new Pen(Color.Silver);
        if (minor >= 6) DrawGridLines(g, minorPen, minor, center);
        if (major >= 6) DrawGridLines(g, majorPen, major, center);
        using var axisPen = new Pen(Color.Gray);
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
        using var blue = new Pen(Color.RoyalBlue, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        using var selected = new Pen(Color.DarkGoldenrod, 3) { StartCap = System.Drawing.Drawing2D.LineCap.Round, EndCap = System.Drawing.Drawing2D.LineCap.Round };
        for (int p = 0; p < _pieces.Count; p++)
        {
            var piece = _pieces[p];
            var pen = p == _selectedPieceIndex ? selected : blue;
            for (int i = 1; i < piece.Vertices.Count; i++) DrawSegment(g, pen, piece.Vertices[i - 1], piece.Vertices[i], true);
            if (piece.Vertices.Count > 0) DrawQuantity(g, piece);
            foreach (var vertex in piece.Vertices)
            {
                Point point = ModelToScreen(vertex);
                using var brush = new SolidBrush(p == _selectedPieceIndex ? Color.Goldenrod : Color.Black);
                g.FillEllipse(brush, point.X - 4, point.Y - 4, 8, 8);
            }
        }
        if (_drawingStart != null && _previewPoint != null)
        {
            using var preview = new Pen(Color.DimGray, 2);
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
        g.FillRectangle(Brushes.White, x - size.Width / 2 - 3, y - size.Height / 2 - 2, size.Width + 6, size.Height + 4);
        g.DrawString(text, Font, Brushes.Black, x - size.Width / 2, y - size.Height / 2);
    }

    private void DrawQuantity(Graphics g, CustomTrimPieceState piece)
    {
        Point point = ModelToScreen(piece.Vertices[0]);
        g.DrawString($"Qty {piece.Quantity}", Font, Brushes.Black, point.X + 8, point.Y - 18);
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
        using var pen = new Pen(Color.DarkGoldenrod, 2);
        g.DrawLine(pen, start, end);
        g.DrawString("Color side", Font, Brushes.DarkGoldenrod, end.X + 6, end.Y - 6);
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

    private static CustomTrimPieceState CopyPiece(CustomTrimPieceState piece) => new(piece.Quantity, piece.Vertices.Select(p => new PointF(p.X, p.Y)).ToList());
    private static float Mod(float value, float modulus) => (value % modulus + modulus) % modulus;
    private static double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
    private static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Money(double value) => value.ToString("C", CultureInfo.GetCultureInfo("en-US"));

    private readonly record struct VertexHit(int PieceIndex, int VertexIndex, PointF Vertex);
    private readonly record struct Segment(PointF From, PointF To, double Length);
}
