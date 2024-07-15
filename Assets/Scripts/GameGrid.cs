using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
namespace Match3
{
    public class GameGrid : MonoBehaviour
    {
        [System.Serializable]
        public struct PiecePrefab
        {
            public PieceType type;
            public GameObject prefab;
        };

        [System.Serializable]
        public struct PiecePosition
        {
            public PieceType type;
            public int x;
            public int y;
        };
        private bool _isSwapping = false;

        public int xDim;
        public int yDim;
        public float fillTime;

        public Level level;

        public PiecePrefab[] piecePrefabs;
        public GameObject backgroundPrefab;

        public PiecePosition[] initialPieces;

        private Dictionary<PieceType, GameObject> _piecePrefabDict;

        private GamePiece[,] _pieces;

        private bool _inverse;

        private GamePiece _pressedPiece;
        private GamePiece _enteredPiece;

        private bool _gameOver;

        public bool IsFilling { get; private set; }

        private void Awake()
        {
            // populating dictionary with piece prefabs types
            _piecePrefabDict = new Dictionary<PieceType, GameObject>();
            for (int i = 0; i < piecePrefabs.Length; i++)
            {
                if (!_piecePrefabDict.ContainsKey(piecePrefabs[i].type))
                {
                    _piecePrefabDict.Add(piecePrefabs[i].type, piecePrefabs[i].prefab);
                }
            }

            // instantiate backgrounds
            for (int x = 0; x < xDim; x++)
            {
                for (int y = 0; y < yDim; y++)
                {
                    GameObject background = Instantiate(backgroundPrefab, GetWorldPosition(x, y), Quaternion.identity);
                    background.transform.parent = transform;
                }
            }

            // instantiating pieces
            _pieces = new GamePiece[xDim, yDim];

            for (int i = 0; i < initialPieces.Length; i++)
            {
                if (initialPieces[i].x >= 0 && initialPieces[i].y < xDim
                                            && initialPieces[i].y >=0 && initialPieces[i].y <yDim)
                {
                    SpawnNewPiece(initialPieces[i].x, initialPieces[i].y, initialPieces[i].type);
                }
            }

            for (int x = 0; x < xDim; x++)
            {
                for (int y = 0; y < yDim; y++)
                {
                    if (_pieces[x, y] == null)
                    {
                        SpawnNewPiece(x, y, PieceType.Empty);
                    }                
                }
            }

            StartCoroutine(Fill());
        }

        private IEnumerator Fill()
        {
            _isSwapping = true; // Prevent interaction while filling
            bool needsRefill = true;
            IsFilling = true;

            while (needsRefill)
            {
                yield return new WaitForSeconds(fillTime);
                while (FillStep())
                {
                    _inverse = !_inverse;
                    yield return new WaitForSeconds(fillTime);
                }

                needsRefill = ClearAllValidMatches();
            }

            IsFilling = false;
            _isSwapping = false; // Allow interaction after filling is complete
        }


        /// <summary>
        /// One pass through all grid cells, moving them down one grid, if possible.
        /// </summary>
        /// <returns> returns true if at least one piece is moved down</returns>
        private bool FillStep()
        {
            bool movedPiece = false;
            // y = 0 is at the top, we ignore the last row, since it can't be moved down.
            for (int y = yDim - 2; y >= 0; y--)
            {
                for (int loopX = 0; loopX < xDim; loopX++)
                {
                    int x = _inverse ? xDim - 1 - loopX : loopX;
                    GamePiece piece = _pieces[x, y];

                    if (!piece.IsMovable()) continue;
                
                    GamePiece pieceBelow = _pieces[x, y + 1];

                    if (pieceBelow.Type == PieceType.Empty)
                    {
                        Destroy(pieceBelow.gameObject);
                        piece.MovableComponent.Move(x, y + 1, fillTime);
                        _pieces[x, y + 1] = piece;
                        SpawnNewPiece(x, y, PieceType.Empty);
                        movedPiece = true;
                    }
                    else
                    {
                        for (int diag = -1; diag <= 1; diag++)
                        {
                            if (diag == 0) continue;
                        
                            int diagX = _inverse ? x - diag : x + diag;

                            if (diagX < 0 || diagX >= xDim) continue;
                        
                            GamePiece diagonalPiece = _pieces[diagX, y + 1];

                            if (diagonalPiece.Type != PieceType.Empty) continue;
                        
                            bool hasPieceAbove = true;

                            for (int aboveY = y; aboveY >= 0; aboveY--)
                            {
                                GamePiece pieceAbove = _pieces[diagX, aboveY];

                                if (pieceAbove.IsMovable())
                                {
                                    break;
                                }
                                else if (pieceAbove.Type != PieceType.Empty)
                                {
                                    hasPieceAbove = false;
                                    break;
                                }
                            }

                            if (hasPieceAbove) continue;
                        
                            Destroy(diagonalPiece.gameObject);
                            piece.MovableComponent.Move(diagX, y + 1, fillTime);
                            _pieces[diagX, y + 1] = piece;
                            SpawnNewPiece(x, y, PieceType.Empty);
                            movedPiece = true;
                            break;
                        }
                    }
                }
            }

            // the highest row (0) is a special case, we must fill it with new pieces if empty
            for (int x = 0; x < xDim; x++)
            {
                GamePiece pieceBelow = _pieces[x, 0];

                if (pieceBelow.Type != PieceType.Empty) continue;
            
                Destroy(pieceBelow.gameObject);
                GameObject newPiece = Instantiate(_piecePrefabDict[PieceType.Normal], GetWorldPosition(x, -1), Quaternion.identity, this.transform);

                _pieces[x, 0] = newPiece.GetComponent<GamePiece>();
                _pieces[x, 0].Init(x, -1, this, PieceType.Normal);
                _pieces[x, 0].MovableComponent.Move(x, 0, fillTime);
                _pieces[x, 0].ColorComponent.SetColor((ColorType)Random.Range(0, _pieces[x, 0].ColorComponent.NumColors));
                movedPiece = true;
            }

            return movedPiece;
        }


        public Vector2 GetWorldPosition(int x, int y)
        {
            return new Vector2(
                transform.position.x - xDim / 2.0f + x,
                transform.position.y + yDim / 2.0f - y);
        }

        private GamePiece SpawnNewPiece(int x, int y, PieceType type)
        {
            Vector3 worldPosition = GetWorldPosition(x, y);
            Debug.Log($"Spawning {type} piece at grid position ({x}, {y}), world position: {worldPosition}");

            GameObject newPiece = Instantiate(_piecePrefabDict[type], worldPosition, Quaternion.identity, this.transform); 
            _pieces[x, y] = newPiece.GetComponent<GamePiece>();
            _pieces[x, y].Init(x, y, this, type);

            Debug.Log($"New piece local position: {newPiece.transform.localPosition}");

            return _pieces[x, y];
        }

        private static bool IsAdjacent(GamePiece piece1, GamePiece piece2) =>
            (piece1.X == piece2.X && Mathf.Abs(piece1.Y - piece2.Y) == 1) ||
            (piece1.Y == piece2.Y && Mathf.Abs(piece1.X - piece2.X) == 1);


        private void SwapPieces(GamePiece piece1, GamePiece piece2)
        {
            if (_gameOver || _isSwapping) return;

            if (!piece1.IsMovable() || !piece2.IsMovable()) return;
            _isSwapping = true;

            // Store original positions
            Vector3 piece1OriginalPosition = piece1.transform.position;
            Vector3 piece2OriginalPosition = piece2.transform.position;

            // Swap pieces in the grid array
            _pieces[piece1.X, piece1.Y] = piece2;
            _pieces[piece2.X, piece2.Y] = piece1;

            // Determine if the swap creates a match
            var (match1, isHorizontal1) = GetMatch(piece1, piece2.X, piece2.Y);
            var (match2, isHorizontal2) = GetMatch(piece2, piece1.X, piece1.Y);

            bool isValidSwap = match1 != null || match2 != null ||
                            piece1.Type == PieceType.Rainbow ||
                            piece2.Type == PieceType.Rainbow ||
                            piece1.Type == PieceType.ColorChanger ||
                            piece2.Type == PieceType.ColorChanger;

            // Adjust these durations to make the animations slower
            float swapDuration = 0.2f; // Increased from fillTime
            float revertDuration = 0.2f;

            DOTween.Pause(piece1.transform);
            DOTween.Pause(piece2.transform);

            // Animate the swap
            Sequence swapSequence = DOTween.Sequence();
            swapSequence.Join(piece1.transform.DOMove(piece2OriginalPosition, swapDuration).SetEase(Ease.OutQuad));
            swapSequence.Join(piece2.transform.DOMove(piece1OriginalPosition, swapDuration).SetEase(Ease.OutQuad));

            swapSequence.OnComplete(() =>
            {
                if (isValidSwap)
                {
                    // Handle valid swap
                    int piece1X = piece1.X;
                    int piece1Y = piece1.Y;

                    piece1.X = piece2.X;
                    piece1.Y = piece2.Y;
                    piece2.X = piece1X;
                    piece2.Y = piece1Y;

                    // Handle special pieces and clear matches
                    HandleSpecialPieces(piece1, piece2);
                    ClearAllValidMatches();
                    StartCoroutine(Fill());

                    // Animate any new special pieces
                    if (piece1.Type == PieceType.RowClear || piece1.Type == PieceType.ColumnClear)
                    {
                        AnimateSpecialPiece(piece1);
                    }
                    if (piece2.Type == PieceType.RowClear || piece2.Type == PieceType.ColumnClear)
                    {
                        AnimateSpecialPiece(piece2);
                    }

                    _pressedPiece = null;
                    _enteredPiece = null;

                    level.OnMove();
                }
                else
                {
                    // Swap back if not a valid match
                    _pieces[piece1.X, piece1.Y] = piece1;
                    _pieces[piece2.X, piece2.Y] = piece2;

                    Sequence revertSequence = DOTween.Sequence();
                    revertSequence.Join(piece1.transform.DOMove(piece1OriginalPosition, revertDuration).SetEase(Ease.InQuad));
                    revertSequence.Join(piece2.transform.DOMove(piece2OriginalPosition, revertDuration).SetEase(Ease.InQuad));
                    revertSequence.OnComplete(() => 
                    {
                        _isSwapping = false;
                        DOTween.Play(piece1.transform);
                        DOTween.Play(piece2.transform);
                    });
                }
                _isSwapping = false;
            });
        }


        private void AnimateSpecialPiece(GamePiece piece)
        {
            if (piece.Type == PieceType.RowClear || piece.Type == PieceType.ColumnClear)
            {
                float duration = 1f;
                float swingOffset = piece.transform.localScale.x * 0.16f;
                Vector3 startPosition = piece.transform.localPosition;
                float moveDownOffset = 0.5f;

                DOTween.Kill(piece.transform);

                Sequence animationSequence = DOTween.Sequence();

                animationSequence.Append(piece.transform.DOLocalMoveY(startPosition.y - moveDownOffset, 0.3f).SetEase(Ease.InOutQuad))
                    .AppendCallback(() =>
                    {
                        if (piece.Type == PieceType.RowClear)
                        {
                            animationSequence.Append(piece.transform.DOLocalMoveX(startPosition.x - swingOffset / 2, 0.2f).SetEase(Ease.InOutSine))
                                .Append(piece.transform.DOLocalMoveX(startPosition.x + swingOffset / 2, 0.4f).SetEase(Ease.InOutSine))
                                .Append(piece.transform.DOLocalMoveX(startPosition.x - swingOffset / 2, 0.4f).SetEase(Ease.InOutSine))
                                .Append(piece.transform.DOLocalMoveX(startPosition.x, 0.2f).SetEase(Ease.InOutSine));
                        }
                        else
                        {
                            animationSequence.Append(piece.transform.DOLocalMoveY(startPosition.y - moveDownOffset - swingOffset / 2, 0.2f).SetEase(Ease.InOutSine))
                                .Append(piece.transform.DOLocalMoveY(startPosition.y - moveDownOffset + swingOffset / 2, 0.4f).SetEase(Ease.InOutSine))
                                .Append(piece.transform.DOLocalMoveY(startPosition.y - moveDownOffset - swingOffset / 2, 0.4f).SetEase(Ease.InOutSine))
                                .Append(piece.transform.DOLocalMoveY(startPosition.y - moveDownOffset, 0.2f).SetEase(Ease.InOutSine));
                        }

                        animationSequence.SetLoops(-1, LoopType.Restart);
                    });

                Debug.Log($"Started animation for {piece.Type} at position: {startPosition}");
            }
        }





        private void HandleSpecialPieces(GamePiece piece1, GamePiece piece2)
        {
            var (match1, isHorizontal1) = GetMatch(piece1, piece1.X, piece1.Y);
            var (match2, isHorizontal2) = GetMatch(piece2, piece2.X, piece2.Y);

            // Handle Rainbow piece interactions
            if (piece1.Type == PieceType.Rainbow && piece2.IsColored())
            {
                ClearColor(piece2.ColorComponent.Color);
                ClearPiece(piece1.X, piece1.Y);
            }
            else if (piece2.Type == PieceType.Rainbow && piece1.IsColored())
            {
                ClearColor(piece1.ColorComponent.Color);
                ClearPiece(piece2.X, piece2.Y);
            }
            // Handle Color Changer interactions
            else if (piece1.Type == PieceType.ColorChanger && piece2.IsColored())
            {
                ColorChangerPiece colorChanger = piece1.GetComponent<ColorChangerPiece>();
                if (colorChanger != null)
                {
                    HandleColorChanger(colorChanger, piece2);
                }
            }
            else if (piece2.Type == PieceType.ColorChanger && piece1.IsColored())
            {
                ColorChangerPiece colorChanger = piece2.GetComponent<ColorChangerPiece>();
                if (colorChanger != null)
                {
                    HandleColorChanger(colorChanger, piece1);
                }
            }
            // Handle Row and Column Clear pieces
            else if (match1 != null || match2 != null)
            {
                if (piece1.Type == PieceType.RowClear)
                {
                    ClearRow(piece1.Y);
                    ClearPiece(piece1.X, piece1.Y);
                }

                if (piece1.Type == PieceType.ColumnClear)
                {
                    ClearColumn(piece1.X);
                    ClearPiece(piece1.X, piece1.Y);
                }

                if (piece2.Type == PieceType.RowClear)
                {
                    ClearRow(piece2.Y);
                    ClearPiece(piece2.X, piece2.Y);
                }

                if (piece2.Type == PieceType.ColumnClear)
                {
                    ClearColumn(piece2.X);
                    ClearPiece(piece2.X, piece2.Y);
                }
            }

            if ((piece1.Type == PieceType.RowClear && piece2.Type == PieceType.ColumnClear) ||
                (piece2.Type == PieceType.RowClear && piece1.Type == PieceType.ColumnClear))
            {
                ClearCross(piece1.X, piece1.Y, piece2.X, piece2.Y);
                ClearPiece(piece1.X, piece1.Y);
                ClearPiece(piece2.X, piece2.Y);
            }
            else if (piece1.Type == PieceType.RowClear && piece2.Type == PieceType.RowClear)
            {
                ClearRow(piece1.Y);
                ClearRow(piece2.Y);
                ClearPiece(piece1.X, piece1.Y);
                ClearPiece(piece2.X, piece2.Y);
            }
            else if (piece1.Type == PieceType.ColumnClear && piece2.Type == PieceType.ColumnClear)
            {
                ClearColumn(piece1.X);
                ClearColumn(piece2.X);
                ClearPiece(piece1.X, piece1.Y);
                ClearPiece(piece2.X, piece2.Y);
            }
        }

        private void ClearCross(int x1, int y1, int x2, int y2)
        {
            ClearRow(y1);
            ClearRow(y2);
            ClearColumn(x1);
            ClearColumn(x2);
        }

        private void HandleColorChanger(ColorChangerPiece colorChanger, GamePiece otherPiece)
        {
            ColorType targetColor = otherPiece.ColorComponent.Color;
            PieceType targetType = otherPiece.Type;

            for (int x = 0; x < xDim; x++)
            {
                for (int y = 0; y < yDim; y++)
                {
                    GamePiece piece = _pieces[x, y];
                    if (piece.IsColored() && 
                        (piece.ColorComponent.Color == colorChanger.Color || 
                        piece.ColorComponent.Color == targetColor))
                    {
                        if (targetType == PieceType.Normal)
                        {
                            piece.ColorComponent.SetColor(targetColor);
                        }
                        else
                        {
                            // Convert to special candy and activate
                            Destroy(piece.gameObject);
                            GamePiece newPiece = SpawnNewPiece(x, y, targetType);
                            if (newPiece.IsColored())
                            {
                                newPiece.ColorComponent.SetColor(targetColor);
                            }
                            ClearPiece(x, y);
                        }
                    }
                }
            }

            // Clear the Color Changer piece
            ClearPiece(colorChanger.X, colorChanger.Y);
        }
        public void PressPiece(GamePiece piece) => _pressedPiece = piece;

        public void EnterPiece(GamePiece piece) => _enteredPiece = piece;

        public void ReleasePiece()
        {
            if (_isSwapping) return;

            if (IsAdjacent(_pressedPiece, _enteredPiece))
            {
                SwapPieces(_pressedPiece, _enteredPiece);
            }
        }

        private bool ClearAllValidMatches()
        {
            bool needsRefill = false;

            for (int y = 0; y < yDim; y++)
            {
                for (int x = 0; x < xDim; x++)
                {
                    if (!_pieces[x, y].IsClearable()) continue;

                    var (match, isHorizontal) = GetMatch(_pieces[x, y], x, y);

                    if (match == null) continue;

                    PieceType specialPieceType = PieceType.Count;
                    GamePiece movingPiece = _pressedPiece == _pieces[x, y] ? _enteredPiece : _pressedPiece;
                    int specialPieceX = movingPiece != null ? movingPiece.X : x;
                    int specialPieceY = movingPiece != null ? movingPiece.Y : y;

                    // Determine special piece type based on match
                    if (match.Count == 4)
                    {
                        specialPieceType = isHorizontal ? PieceType.ColumnClear : PieceType.RowClear;
                    }
                    else if (match.Count >= 5)
                    {
                        specialPieceType = PieceType.Rainbow;
                    }

                    foreach (var piece in match)
                    {
                        if (ClearPiece(piece.X, piece.Y))
                        {
                            needsRefill = true;
                        }
                    }

                    // Create special piece if applicable
                    if (specialPieceType != PieceType.Count)
                    {
                        Destroy(_pieces[specialPieceX, specialPieceY].gameObject);
                        GamePiece newPiece = SpawnNewPiece(specialPieceX, specialPieceY, specialPieceType);

                        if (newPiece.IsColored() && match[0].IsColored())
                        {
                            newPiece.ColorComponent.SetColor(match[0].ColorComponent.Color);
                        }

                        // Animate the new special piece
                        AnimateSpecialPiece(newPiece);
                    }
                }
            }

            return needsRefill;
        }





        private (List<GamePiece>, bool) GetMatch(GamePiece piece, int newX, int newY)
    {
        if (!piece.IsColored()) return (null,false);
        var color = piece.ColorComponent.Color;
        var horizontalPieces = new List<GamePiece>();
        var verticalPieces = new List<GamePiece>();
        var matchingPieces = new List<GamePiece>();
        bool isHorizontalMatch = false;

        // First check horizontal
        horizontalPieces.Add(piece);

        for (int dir = 0; dir <= 1; dir++)
        {
            for (int xOffset = 1; xOffset < xDim; xOffset++)
            {
                int x;

                if (dir == 0)
                { // Left
                    x = newX - xOffset;
                }
                else
                { // right
                    x = newX + xOffset;                        
                }

                // out-of-bounds
                if (x < 0 || x >= xDim) { break; }

                // piece is the same color?
                if (_pieces[x, newY].IsColored() && _pieces[x, newY].ColorComponent.Color == color)
                {
                    horizontalPieces.Add(_pieces[x, newY]);
                }
                else
                {
                    break;
                }
            }
        }
        if (horizontalPieces.Count == 5)
        {
            GamePiece middlePiece = horizontalPieces[2];
            for (int yOffset = -1; yOffset <= 1; yOffset += 2)
            {
                int y = middlePiece.Y + yOffset;
                if (y >= 0 && y < yDim && _pieces[middlePiece.X, y].IsColored() && _pieces[middlePiece.X, y].ColorComponent.Color == color)
                {
                    Debug.Log("Altılı");
                    horizontalPieces.Add(_pieces[middlePiece.X, y]);
                    return (horizontalPieces,true); // Return 6-match immediately
                }
            }
        }

        if (horizontalPieces.Count >= 3)
        {
            matchingPieces.AddRange(horizontalPieces);
            isHorizontalMatch = true;

        }

        // Traverse vertically if we found a match (for L and T shape)
        if (horizontalPieces.Count >= 3)
        {
            for (int i = 0; i < horizontalPieces.Count; i++ )
            {
                for (int dir = 0; dir <= 1; dir++)
                {
                    for (int yOffset = 1; yOffset < yDim; yOffset++)                        
                    {
                        int y;
                        
                        if (dir == 0)
                        { // Up
                            y = newY - yOffset;
                        }
                        else
                        { // Down
                            y = newY + yOffset;
                        }

                        if (y < 0 || y >= yDim)
                        {
                            break;
                        }

                        if (_pieces[horizontalPieces[i].X, y].IsColored() && _pieces[horizontalPieces[i].X, y].ColorComponent.Color == color)
                        {
                            verticalPieces.Add(_pieces[horizontalPieces[i].X, y]);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (verticalPieces.Count < 2)
                {
                    verticalPieces.Clear();
                }
                else
                {
                    matchingPieces.AddRange(verticalPieces);
                    break;
                }
            }
        }

        if (matchingPieces.Count >= 3)
        {
            return (matchingPieces, isHorizontalMatch);
        }


        // Didn't find anything going horizontally first,
        // so now check vertically
        horizontalPieces.Clear();
        verticalPieces.Clear();
        verticalPieces.Add(piece);

        for (int dir = 0; dir <= 1; dir++)
        {
            for (int yOffset = 1; yOffset < xDim; yOffset++)
            {
                int y;

                if (dir == 0)
                { // Up
                    y = newY - yOffset;
                }
                else
                { // Down
                    y = newY + yOffset;                        
                }

                // out-of-bounds
                if (y < 0 || y >= yDim) { break; }

                // piece is the same color?
                if (_pieces[newX, y].IsColored() && _pieces[newX, y].ColorComponent.Color == color)
                {
                    verticalPieces.Add(_pieces[newX, y]);
                }
                else
                {
                    break;
                }
            }
        }

        if (verticalPieces.Count >= 3)
        {
            matchingPieces.AddRange(verticalPieces);
        }

        // Traverse horizontally if we found a match (for L and T shape)
        if (verticalPieces.Count >= 3)
        {
            for (int i = 0; i < verticalPieces.Count; i++)
            {
                for (int dir = 0; dir <= 1; dir++)
                {
                    for (int xOffset = 1; xOffset < yDim; xOffset++)
                    {
                        int x;

                        if (dir == 0)
                        { // Left
                            x = newX - xOffset;
                        }
                        else
                        { // Right
                            x = newX + xOffset;
                        }

                        if (x < 0 || x >= xDim)
                        {
                            break;
                        }

                        if (_pieces[x, verticalPieces[i].Y].IsColored() && _pieces[x, verticalPieces[i].Y].ColorComponent.Color == color)
                        {
                            horizontalPieces.Add(_pieces[x, verticalPieces[i].Y]);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                if (horizontalPieces.Count < 2)
                {
                    horizontalPieces.Clear();
                }
                else
                {
                    matchingPieces.AddRange(horizontalPieces);
                    break;
                }
            }
        }

        if (matchingPieces.Count >= 3)
        {
            return (matchingPieces, isHorizontalMatch);
        }

        return (null, false);;
    }
        public void RemoveSinglePieceMatch(GamePiece piece)
        {
            if (piece == null) return;

            var (match, isHorizontal) = GetMatch(piece, piece.X, piece.Y);
            
            if (match != null)
            {
                foreach (GamePiece matchPiece in match)
                {
                    if (matchPiece != null)
                    {
                        ClearPiece(matchPiece.X, matchPiece.Y);
                    }
                }

                StartCoroutine(Fill());
            }

            // Debug output
            Debug.Log($"Attempted to remove match at ({piece.X}, {piece.Y}). Match count: {match?.Count ?? 0}");
        }

        private bool ClearPiece(int x, int y)
        {
            if (!_pieces[x, y].IsClearable() || _pieces[x, y].ClearableComponent.IsBeingCleared) return false;

            _pieces[x, y].ClearableComponent.Clear();

            // Animate the piece shrinking and fading out
            _pieces[x, y].transform.DOScale(Vector3.zero, fillTime / 2)
                .SetEase(Ease.InQuad)
                .OnComplete(() =>
                {
                    Destroy(_pieces[x, y].gameObject);
                    SpawnNewPiece(x, y, PieceType.Empty);
                });

            ClearObstacles(x, y);

            return true;
        }

        private void ClearObstacles(int x, int y)
        {
            for (int adjacentX = x - 1; adjacentX <= x + 1; adjacentX++)
            {
                if (adjacentX == x || adjacentX < 0 || adjacentX >= xDim) continue;

                if (_pieces[adjacentX, y].Type != PieceType.Bubble || !_pieces[adjacentX, y].IsClearable()) continue;
            
                _pieces[adjacentX, y].ClearableComponent.Clear();
                SpawnNewPiece(adjacentX, y, PieceType.Empty);
            }

            for (int adjacentY = y - 1; adjacentY <= y + 1; adjacentY++)
            {
                if (adjacentY == y || adjacentY < 0 || adjacentY >= yDim) continue;

                if (_pieces[x, adjacentY].Type != PieceType.Bubble || !_pieces[x, adjacentY].IsClearable()) continue;
            
                _pieces[x, adjacentY].ClearableComponent.Clear();
                SpawnNewPiece(x, adjacentY, PieceType.Empty);
            }
        }

        public void ClearRow(int row)
        {
            for (int x = 0; x < xDim; x++)
            {
                ClearPiece(x, row);
            }
        }

        public void ClearColumn(int column)
        {
            for (int y = 0; y < yDim; y++)
            {
                ClearPiece(column, y);
            }
        }

        public void ClearColor(ColorType color)
        {
            for (int x = 0; x < xDim; x++)
            {
                for (int y = 0; y < yDim; y++)
                {
                    if ((_pieces[x, y].IsColored() && _pieces[x, y].ColorComponent.Color == color)
                        || (color == ColorType.Any))
                    {
                        ClearPiece(x, y);
                    }
                }
            }
        }

        public void GameOver() => _gameOver = true;

        public List<GamePiece> GetPiecesOfType(PieceType type)
        {
            var piecesOfType = new List<GamePiece>();

            for (int x = 0; x < xDim; x++)
            {
                for (int y = 0; y < yDim; y++)
                {
                    if (_pieces[x, y].Type == type)
                    {
                        piecesOfType.Add(_pieces[x, y]);
                    }
                }
            }

            return piecesOfType;
        }

    }
}
