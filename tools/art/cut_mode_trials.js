// Cuts the Trial Tower card painting (Resources/UI/mode_trials.png) from the owner's own Summon Reveal mockup:
// the lit pagoda on its waterfall cliff under the purple spirit swirl, left of the hero card. The cut is tall and
// narrow (the card's own shape) with the pagoda in the middle, so the card's crop-to-fill never cuts it off.
// usage: NODE_PATH=<dir with sharp> node tools/art/cut_mode_trials.js <folder holding 07_Summon_Reveal.png>
const sharp = require("sharp");
const path = require("path");
const source = path.join(process.argv[2], "07_Summon_Reveal.png");
const out = path.join(__dirname, "..", "..", "unity", "Assets", "_Game", "Resources", "UI", "mode_trials.png");
sharp(source)
  .extract({ left: 16, top: 380, width: 194, height: 360 })
  .resize(388, 720, { kernel: "lanczos3" })
  .png()
  .toFile(out)
  .then(() => console.log("wrote", out));
