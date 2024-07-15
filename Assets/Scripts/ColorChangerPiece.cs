using UnityEngine;

namespace Match3
{
    public class ColorChangerPiece : ClearablePiece
    {
        public ColorType Color { get; private set; }
        private GamePiece _gamePiece;

        private void Awake()
        {
            _gamePiece = GetComponent<GamePiece>();
        }

        public void SetColor(ColorType color)
        {
            Color = color;
            GetComponent<ColorPiece>().SetColor(color);
        }

        public override void Clear()
        {
            base.Clear();
            // The special clear behavior is handled in the HandleColorChanger method
        }

        // Add these properties to access X and Y through the GamePiece
        public int X => _gamePiece.X;
        public int Y => _gamePiece.Y;
    }
}