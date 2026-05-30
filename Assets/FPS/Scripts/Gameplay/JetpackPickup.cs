namespace Unity.FPS.Gameplay
{
    // ============================================================================
    // JetpackPickup — разовый предмет: разблокирует джетпак. Если уже разблокирован —
    // не подбирается (TryUnlock вернёт false). Это поведение даёт правильную игровую
    // логику: если игрок ещё не подбирал — может, иначе пикап остаётся в мире.
    // ============================================================================
    public class JetpackPickup : Pickup
    {
        protected override void OnPicked(PlayerCharacterController byPlayer)
        {
            var jetpack = byPlayer.GetComponent<Jetpack>();
            if (!jetpack)
                return;

            // TryUnlock возвращает false если джетпак уже разблокирован.
            if (jetpack.TryUnlock())
            {
                PlayPickupFeedback();
                Destroy(gameObject);
            }
        }
    }
}
