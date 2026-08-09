using Emergence.Model.Environment;
using Emergence.Presentation.Contracts;
using Godot;

namespace Emergence.App;

/// <summary>One draw surface for the field; authoritative cells are data samples, never child nodes.</summary>
public partial class FieldViewport : Control
{
    private EnvironmentPresentationSnapshot? _snapshot;
    private ImageTexture? _texture;
    private Rect2 _fieldRect;
    private LatticeCoordinate? _selection;

    public Action<LatticeCoordinate>? CellSelected { get; set; }
    public bool RawGrid { get; private set; }

    public override void _Ready()
    {
        ClipContents = true;
        MouseDefaultCursorShape = CursorShape.Cross;
        TextureFilter = TextureFilterEnum.Linear;
        Resized += QueueRedraw;
    }

    public void SetSnapshot(EnvironmentPresentationSnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _texture = CreateSmoothTexture(snapshot);
        QueueRedraw();
    }

    public void SetRawGrid(bool enabled)
    {
        RawGrid = enabled;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("081217"));
        if (_snapshot is null || _texture is null) return;
        _fieldRect = FitAspect(Size, _snapshot.Width / (float)_snapshot.Height);
        DrawTextureRect(_texture, _fieldRect, tile: false);
        DrawRect(_fieldRect, new Color("6f8991"), filled: false, width: 2f, antialiased: true);

        if (RawGrid)
        {
            Color grid = new("8dc6d4", 0.45f);
            for (uint x = 0; x <= _snapshot.Width; x++)
            {
                float px = _fieldRect.Position.X + (_fieldRect.Size.X * x / _snapshot.Width);
                DrawLine(new(px, _fieldRect.Position.Y), new(px, _fieldRect.End.Y), grid, 1f, antialiased: true);
            }
            for (uint y = 0; y <= _snapshot.Height; y++)
            {
                float py = _fieldRect.Position.Y + (_fieldRect.Size.Y * y / _snapshot.Height);
                DrawLine(new(_fieldRect.Position.X, py), new(_fieldRect.End.X, py), grid, 1f, antialiased: true);
            }
        }

        if (_selection.HasValue)
        {
            LatticeCoordinate cell = _selection.Value;
            Vector2 cellSize = new(_fieldRect.Size.X / _snapshot.Width, _fieldRect.Size.Y / _snapshot.Height);
            Rect2 selected = new(_fieldRect.Position + new Vector2(cell.X * cellSize.X, cell.Y * cellSize.Y), cellSize);
            DrawRect(selected, new Color("f1d879"), filled: false, width: 3f, antialiased: true);
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_snapshot is null || @event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click
            || !_fieldRect.HasPoint(click.Position)) return;
        Vector2 relative = (click.Position - _fieldRect.Position) / _fieldRect.Size;
        uint x = Math.Min(_snapshot.Width - 1, (uint)Math.Floor(relative.X * _snapshot.Width));
        uint y = Math.Min(_snapshot.Height - 1, (uint)Math.Floor(relative.Y * _snapshot.Height));
        _selection = new(x, y);
        CellSelected?.Invoke(_selection.Value);
        QueueRedraw();
        AcceptEvent();
    }

    private static ImageTexture CreateSmoothTexture(EnvironmentPresentationSnapshot snapshot)
    {
        const int textureWidth = 640;
        int textureHeight = checked((int)Math.Round(textureWidth * snapshot.Height / (double)snapshot.Width));
        Image image = Image.CreateEmpty(textureWidth, textureHeight, useMipmaps: false, Image.Format.Rgba8);
        for (int py = 0; py < textureHeight; py++)
        for (int px = 0; px < textureWidth; px++)
        {
            double sampleX = (((px + 0.5) / textureWidth) * snapshot.Width) - 0.5;
            double sampleY = (((py + 0.5) / textureHeight) * snapshot.Height) - 0.5;
            int cellX = Math.Clamp((int)Math.Floor((px / (double)textureWidth) * snapshot.Width), 0, checked((int)snapshot.Width - 1));
            int cellY = Math.Clamp((int)Math.Floor((py / (double)textureHeight) * snapshot.Height), 0, checked((int)snapshot.Height - 1));
            int cellIndex = checked((cellY * (int)snapshot.Width) + cellX);
            if (snapshot.SolidMask[cellIndex])
            {
                image.SetPixel(px, py, new Color("1b2a30"));
                continue;
            }
            double value = BilinearFluidSample(snapshot, sampleX, sampleY);
            image.SetPixel(px, py, FieldColor(value));
        }
        return ImageTexture.CreateFromImage(image);
    }

    private static double BilinearFluidSample(EnvironmentPresentationSnapshot snapshot, double x, double y)
    {
        int x0 = Math.Clamp((int)Math.Floor(x), 0, checked((int)snapshot.Width - 1));
        int y0 = Math.Clamp((int)Math.Floor(y), 0, checked((int)snapshot.Height - 1));
        int x1 = Math.Min(x0 + 1, checked((int)snapshot.Width - 1));
        int y1 = Math.Min(y0 + 1, checked((int)snapshot.Height - 1));
        double tx = Math.Clamp(x - Math.Floor(x), 0d, 1d);
        double ty = Math.Clamp(y - Math.Floor(y), 0d, 1d);
        (int X, int Y, double Weight)[] samples =
        [
            (x0, y0, (1d - tx) * (1d - ty)),
            (x1, y0, tx * (1d - ty)),
            (x0, y1, (1d - tx) * ty),
            (x1, y1, tx * ty),
        ];
        double weighted = 0d;
        double weights = 0d;
        foreach ((int sx, int sy, double weight) in samples)
        {
            int index = checked((sy * (int)snapshot.Width) + sx);
            if (snapshot.SolidMask[index]) continue;
            weighted += snapshot.NormalizedSurface[index] * weight;
            weights += weight;
        }
        return weights == 0d ? 0d : Math.Clamp(weighted / weights, 0d, 1d);
    }

    private static Color FieldColor(double value)
    {
        Color low = new("123340");
        Color middle = new("1b8b88");
        Color high = new("e5c66b");
        return value < 0.55
            ? low.Lerp(middle, (float)(value / 0.55))
            : middle.Lerp(high, (float)((value - 0.55) / 0.45));
    }

    private static Rect2 FitAspect(Vector2 available, float aspect)
    {
        float width = available.X;
        float height = width / aspect;
        if (height > available.Y) { height = available.Y; width = height * aspect; }
        return new(new((available.X - width) / 2f, (available.Y - height) / 2f), new(width, height));
    }
}
