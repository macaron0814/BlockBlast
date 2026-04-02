using UnityEngine;

namespace BlockBlastGame
{
    public class BlockDragHandler : MonoBehaviour
    {
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
        Vector3 originalPosition;
        Vector3 originalScale;
        bool isDragging;
        Vector2Int lastPreviewPos;

        void Update()
        {
            var state = GameManager.Instance.currentState;
            if (state != GameState.Playing)
            {
                if (isDragging && state == GameState.LineClearing)
                {
                    isDragging = false;
                    boardManager.tilemapController.ClearPreview();
                    if (selectedPiece != null)
                    {
                        selectedPiece.transform.position = originalPosition;
                        selectedPiece.transform.localScale = originalScale;
                        selectedPiece = null;
                    }
                }
                return;
            }

            if (Input.GetMouseButtonDown(0))
                TryPickUp();

            if (isDragging)
                DragUpdate();

            if (Input.GetMouseButtonUp(0) && isDragging)
                TryDrop();
        }

        void TryPickUp()
        {
            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;

            float closestDist = float.MaxValue;
            BlockPiece closest = null;

            foreach (var piece in blockSpawner.currentPieces)
            {
                if (piece == null) continue;
                float dist = Vector3.Distance(mouseWorld, piece.transform.position);
                if (dist < 1.5f && dist < closestDist)
                {
                    closestDist = dist;
                    closest = piece;
                }
            }

            if (closest != null)
            {
                selectedPiece = closest;
                originalPosition = closest.transform.position;
                originalScale = closest.transform.localScale;
                closest.transform.localScale = Vector3.one * dragScale;
                isDragging = true;
                lastPreviewPos = new Vector2Int(-999, -999);
            }
        }

        void DragUpdate()
        {
            if (selectedPiece == null) return;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            mouseWorld.y += dragOffsetY;

            selectedPiece.transform.position = mouseWorld;

            Vector2Int boardPos = boardManager.WorldToBoard(mouseWorld);
            if (boardPos != lastPreviewPos)
            {
                lastPreviewPos = boardPos;
                bool canPlace = boardManager.CanPlaceBlock(selectedPiece.blockData, boardPos);
                boardManager.tilemapController.ShowPreview(
                    selectedPiece.blockData.GetShapeArray(), boardPos, canPlace);
            }
        }

        void TryDrop()
        {
            isDragging = false;
            boardManager.tilemapController.ClearPreview();

            if (selectedPiece == null) return;

            Vector3 mouseWorld = mainCamera.ScreenToWorldPoint(Input.mousePosition);
            mouseWorld.z = 0;
            mouseWorld.y += dragOffsetY;

            Vector2Int boardPos = boardManager.WorldToBoard(mouseWorld);

            if (boardManager.CanPlaceBlock(selectedPiece.blockData, boardPos))
            {
                boardManager.PlaceBlock(selectedPiece.blockData, boardPos);
                blockSpawner.RemovePiece(selectedPiece);
                GameManager.Instance.OnBlockPlacedCallback();
            }
            else
            {
                selectedPiece.transform.position = originalPosition;
                selectedPiece.transform.localScale = originalScale;
            }

            selectedPiece = null;
        }
    }
}
