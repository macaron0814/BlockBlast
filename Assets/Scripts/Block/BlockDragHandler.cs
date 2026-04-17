using UnityEngine;

namespace BlockBlastGame
{
    public class BlockDragHandler : MonoBehaviour
    {
        static readonly Vector2Int InvalidPreviewPos = new Vector2Int(int.MinValue, int.MinValue);

        [Header("References")]
        public BoardManager boardManager;
        public BlockSpawner blockSpawner;
        public Camera mainCamera;

        [Header("Settings")]
        public float dragOffsetY = 1.5f;
        public float snapBackSpeed = 15f;
        public float dragScale = 1f;
        public float idleScale = 0.6f;

        BlockPiece selectedPiece;
        Transform originalParent;
        Vector3 originalPosition;
        Vector3 originalScale;
        Vector3 dragPointerOffset;
        bool isDragging;
        bool previewVisible;
        bool lastPreviewCanPlace;
        Vector2Int lastPreviewPos = InvalidPreviewPos;

        void Update()
        {
            var state = GameManager.Instance.currentState;
            if (state != GameState.Playing)
            {
                if (isDragging)
                    CancelCurrentDrag();

                return;
            }

            if (IsPointerDownThisFrame())
                TryPickUp();

            if (isDragging && IsPointerHeld())
                DragUpdate();

            if (isDragging && IsPointerReleasedThisFrame())
                TryDrop();
        }

        void TryPickUp()
        {
            Vector3 pointerWorld = GetPointerWorldPosition();

            float closestDist = float.MaxValue;
            BlockPiece closest = null;

            foreach (var piece in blockSpawner.currentPieces)
            {
                if (piece == null) continue;
                float dist = Vector3.Distance(pointerWorld, piece.transform.position);
                if (dist < 1.5f && dist < closestDist)
                {
                    closestDist = dist;
                    closest = piece;
                }
            }

            if (closest != null)
            {
                selectedPiece = closest;
                originalParent = closest.transform.parent;
                originalPosition = closest.transform.position;
                originalScale = closest.transform.localScale;
                dragPointerOffset = closest.transform.position - pointerWorld;

                closest.transform.SetParent(null, true);
                closest.transform.localScale = Vector3.Scale(GetBoardMatchedScale(closest), new Vector3(dragScale, dragScale, 1f));

                isDragging = true;
                previewVisible = false;
                lastPreviewCanPlace = false;
                lastPreviewPos = InvalidPreviewPos;

                DragUpdate();
            }
        }

        void DragUpdate()
        {
            if (selectedPiece == null) return;

            selectedPiece.transform.position = GetDraggedPieceWorldPosition(GetPointerWorldPosition());

            Vector2Int boardPos = GetDraggedBoardPosition(selectedPiece);
            if (!boardManager.IsPlacementWithinBounds(selectedPiece.blockData, boardPos))
            {
                ClearPreviewState();
                return;
            }

            bool canPlace = boardManager.CanPlaceBlock(selectedPiece.blockData, boardPos);
            if (!previewVisible || boardPos != lastPreviewPos || canPlace != lastPreviewCanPlace)
            {
                boardManager.tilemapController.ShowPreview(
                    selectedPiece.blockData.GetShapeArray(), boardPos, canPlace);
                previewVisible = true;
                lastPreviewCanPlace = canPlace;
                lastPreviewPos = boardPos;
            }
        }

        void TryDrop()
        {
            isDragging = false;

            if (selectedPiece == null) return;

            selectedPiece.transform.position = GetDraggedPieceWorldPosition(GetPointerWorldPosition());
            Vector2Int boardPos = GetDraggedBoardPosition(selectedPiece);
            ClearPreviewState();

            if (boardManager.IsPlacementWithinBounds(selectedPiece.blockData, boardPos)
                && boardManager.CanPlaceBlock(selectedPiece.blockData, boardPos))
            {
                boardManager.PlaceBlock(selectedPiece.blockData, boardPos);
                blockSpawner.RemovePiece(selectedPiece);
                GameManager.Instance.OnBlockPlacedCallback();
            }
            else
            {
                RestoreSelectedPiece();
            }

            selectedPiece = null;
            originalParent = null;
        }

        void CancelCurrentDrag()
        {
            isDragging = false;
            ClearPreviewState();

            if (selectedPiece == null)
                return;

            RestoreSelectedPiece();
            selectedPiece = null;
            originalParent = null;
        }

        void RestoreSelectedPiece()
        {
            if (selectedPiece == null)
                return;

            selectedPiece.transform.SetParent(originalParent, true);
            selectedPiece.transform.position = originalPosition;
            selectedPiece.transform.localScale = originalScale;
        }

        void ClearPreviewState()
        {
            if (previewVisible)
                boardManager.tilemapController.ClearPreview();

            previewVisible = false;
            lastPreviewCanPlace = false;
            lastPreviewPos = InvalidPreviewPos;
        }

        Vector3 GetDraggedPieceWorldPosition(Vector3 pointerWorld)
        {
            Vector3 draggedWorld = pointerWorld + dragPointerOffset;
            draggedWorld.z = 0f;
            return draggedWorld;
        }

        Vector2Int GetDraggedBoardPosition(BlockPiece piece)
        {
            Vector2 cellStep = GetBoardCellStepWorld();
            bool[,] shape = piece.blockData.GetShapeArray();
            int shapeW = shape.GetLength(0);
            int shapeH = shape.GetLength(1);

            Vector3 anchorWorld = piece.transform.position - new Vector3(
                (shapeW - 1) * cellStep.x * 0.5f,
                (shapeH - 1) * cellStep.y * 0.5f,
                0f);

            return boardManager.WorldToBoard(anchorWorld);
        }

        Vector3 GetBoardMatchedScale(BlockPiece piece)
        {
            if (piece == null || piece.cellSize <= Mathf.Epsilon)
                return Vector3.one;

            Vector2 cellStep = GetBoardCellStepWorld();
            return new Vector3(cellStep.x / piece.cellSize, cellStep.y / piece.cellSize, 1f);
        }

        Vector2 GetBoardCellStepWorld()
        {
            Vector3 origin = boardManager.BoardToWorld(Vector2Int.zero);
            Vector3 stepX = boardManager.BoardToWorld(Vector2Int.right) - origin;
            Vector3 stepY = boardManager.BoardToWorld(Vector2Int.up) - origin;
            return new Vector2(Mathf.Abs(stepX.x), Mathf.Abs(stepY.y));
        }

        Vector3 GetPointerWorldPosition()
        {
            Vector2 screenPosition = Input.touchCount > 0
                ? Input.GetTouch(0).position
                : (Vector2)Input.mousePosition;

            float depth = Mathf.Abs(mainCamera.transform.position.z);
            Vector3 world = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            world.z = 0f;
            return world;
        }

        bool IsPointerDownThisFrame()
        {
            if (Input.touchCount > 0)
                return Input.GetTouch(0).phase == TouchPhase.Began;

            return Input.GetMouseButtonDown(0);
        }

        bool IsPointerHeld()
        {
            if (Input.touchCount > 0)
            {
                TouchPhase phase = Input.GetTouch(0).phase;
                return phase == TouchPhase.Began
                    || phase == TouchPhase.Moved
                    || phase == TouchPhase.Stationary;
            }

            return Input.GetMouseButton(0);
        }

        bool IsPointerReleasedThisFrame()
        {
            if (Input.touchCount > 0)
            {
                TouchPhase phase = Input.GetTouch(0).phase;
                return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
            }

            return Input.GetMouseButtonUp(0);
        }
    }
}
