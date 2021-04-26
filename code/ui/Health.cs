using Sandbox;
using Sandbox.UI;
using Sandbox.UI.Construct;

namespace Prophunt.UI.External
{
    public class Health : Panel
    {
        public Label Label;

        public Health()
        {
            object Health2 = Add.Label("💓", "icon");
            Label = Add.Label("100", "value");
            if (player.Health) = 10f; // if the player health is under 10 change gui to broken heart
                    {
                Health2 = Add.Label("💔", "icon";)
            }



        }

        public override void Tick()
        {
            var player = Player.Local;
            if (player == null) return;

            Label.Text = $"{player.Health:n0}";
        }
    }
}
