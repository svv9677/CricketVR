# CricketVR — Asset Provenance

Every acquired asset, its licence, and which tier of the sourcing waterfall it came from.
Tiers: **1** = free from the internet · **2** = authored in Blender · **3** = purchased.

Sources verified 2026-09-21. All URLs returned HTTP 200/206 on check.

## Licence summary of the sources used

| Source | Licence | Commercial OK | Attribution |
|---|---|---|---|
| ambientCG | CC0 1.0 | Yes | Not required |
| Poly Haven | CC0 | Yes | Not required |
| TextureCan | CC0 1.0 Universal | Yes | Not required |
| cgbookcase | CC0 | Yes | Not required |
| Quaternius | CC0 | Yes | Not required |
| Sketchfab CC-BY items | CC-BY 4.0 | Yes | **Required — maintain a credits screen** |

⚠️ **Avoid ShareTextures** for this project. Its "custom CC0" forbids redistribution in
collections without written permission — shipping in a game is fine, but it muddies otherwise
clean CC0 provenance for no benefit, since ambientCG and Poly Haven cover the same needs.

⚠️ **Do not plan around Quixel Megascans.** Free to all only until 31 Dec 2024; from 2025 it
moved to Fab and is paid, with a *rotating* free subset. An asset free today may not be
free-acquirable later for a teammate or CI machine. It is also film-quality — 4K–8K source with
heavy displacement and scan-density meshes — so every asset would need downrezzing and
decimation before touching a Quest 3.

## Materials (tier 1 unless noted)

| Surface | Source | URL | Notes |
|---|---|---|---|
| Outfield turf | ambientCG **Grass005** | https://ambientcg.com/a/Grass005 | Photogrammetry, short/clean lawn. Best-groomed lawn on the site |
| Turf variation | ambientCG **Grass001**, **Grass004** | https://ambientcg.com/a/Grass001 | Break up tiling |
| Worn/sparse patches | Poly Haven **Sparse Grass** | https://polyhaven.com/a/sparse_grass | Ships a Mask map |
| Pitch strip | Poly Haven **Brown Mud Dry** | https://polyhaven.com/a/brown_mud_dry | Dry, coarse, compacted. Verified: AO, Rough, nor_gl, Displacement all present |
| Pitch, rolled variant | ambientCG **Ground102** | https://ambientcg.com/a/Ground102 | Tagged compressed/stamped |
| Crack network | 3dtextures.me **Cracked Mud 001** | https://3dtextures.me/2018/08/13/cracked-mud-001/ | **1K free only**, 4K is Patreon-gated |
| Concrete | ambientCG **Concrete034**, **Concrete047A** | https://ambientcg.com/a/Concrete034 | 047A adds AO |
| Concrete alt | Poly Haven **Concrete Floor 01** | https://polyhaven.com/a/concrete_floor_01 | Weathered outdoor |
| Seating plastic | ambientCG **Plastic013A/015A/016A/017A** | https://ambientcg.com/a/Plastic015A | White/blue/yellow/green. **No AO map** — packer defaults it to 1.0 |
| Painted metal | ambientCG **PaintedMetal005**, **Metal027/028/029** | https://ambientcg.com/a/PaintedMetal005 | Powder-coated steel |
| Willow (bat) | ambientCG **Wood090A** or **Wood022** | https://ambientcg.com/a/Wood090A | ⚠️ Not actually willow — see gaps |
| Leather (ball) | cgbookcase **Red Leather 01** | https://www.cgbookcase.com/textures/red-leather-01 | Already red, saves a tint pass |
| Cotton (whites) | cgbookcase **blue-cotton-01** | https://www.cgbookcase.com/textures/blue-cotton-01 | Desaturate to white |

## HDRI skies (tier 1)

| Need | Asset | URL |
|---|---|---|
| Day | Poly Haven **Kloofendal 48d Partly Cloudy (Pure Sky)** | https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky |
| Night | Poly Haven **Dikhololo Night** | https://polyhaven.com/a/dikhololo_night |

**Pure Sky** matters — the ground is removed, which is what you want when only sky shows above
the stands. Verified direct 4K link (20.7 MB):
`https://dl.polyhaven.org/file/ph-assets/HDRIs/hdr/4k/kloofendal_48d_partly_cloudy_puresky_4k.hdr`

For a floodlit night match prefer **Dikhololo Night** over Moonless Golf — the latter includes
warm clubhouse lamps that will fight your own floodlight rig. You want the night HDRI
contributing near zero.

## Import rules — read before downloading anything

| Rule | Why |
|---|---|
| Take **`nor_gl`**, never `nor_dx` | Unity uses the OpenGL normal convention. Both sites ship both |
| **Discard every displacement / height map** | No tessellation budget on Quest. Bake detail into the normal instead |
| Download the **2K JPG** variants directly | Smaller download, and skips a Unity reimport at reduced max size |
| Ignore the 4K/8K/16K options | A trap for this project |
| Poly Haven's **`arm` file is NOT usable directly in URP** | See the warning below |

> ⚠️ **The `arm` trap.** Poly Haven ships an `arm` texture packing **AO / Roughness / Metallic**
> into R/G/B. That is the glTF and Unreal convention — it is **not** what URP Lit reads. URP Lit
> wants **Metallic in R** and **Smoothness in A** on the Metallic map, and **Occlusion in G** on
> the Occlusion map. Feeding `arm` straight in gives you AO-as-metallic, roughness-as-occlusion,
> and no smoothness at all.
>
> **Verified against the Poly Haven API** — `brown_mud_dry` exposes `AO`, `Rough`, `nor_gl`,
> `nor_dx`, `Diffuse`, `Displacement`, `arm`, `spec`, `Bump`. Use the **separate `AO` and `Rough`
> maps** as inputs to `OrmPacker` and ignore `arm`. Most of these surfaces are dielectric with no
> metal map at all, which the packer handles by defaulting metallic to 0.

## GAPS — not sourceable free

| # | Gap | Resolution | Tier |
|---|---|---|---|
| 1 | **Spectator crowd atlas.** Nothing free exists — verified. OpenGameArt has crowd *audio* only. Unity Asset Store's Stadium Crowd Generator is $19.99 | Author in Blender from [Quaternius Ultimate Modular Men](https://quaternius.com/packs/ultimatemodularcharacters.html) (CC0): pose seated, randomise shirt colours, render 8–16 yaw angles × ~12 variants to a 2K atlas | **2** |
| 2 | **Mown stripes done correctly.** TextureCan [Ground 0040](https://www.texturecan.com/details/469/) (CC0, includes `.sbsar`) exists but baking stripes is the wrong technique — see the plan's task 24 | Author as a shader function. Keep the sbsar as a visual reference for band contrast | **2** |
| 3 | **Willow.** No free texture is actually cricket-bat willow (near-white, very straight tight grain, no figure). Wood090A is birch — warmer and more figured | Accept with a desaturate + grain contrast push, or author procedurally. Straight parallel grain is easy in Blender nodes | 1 or 2 |
| 4 | **Cotton twill.** No free source has a twill weave; available cottons are plain/basket weave | Accept. At Quest viewing distance the diagonal twill rib is invisible | 1 |
| 5 | **Cricket stadium geometry.** No free, clean, low-poly, non-trademarked cricket stadium exists | Keep the existing `FullStadium.fbx` | n/a |
| 6 | Stadium seating geometry | [Low poly stadium seats](https://sketchfab.com/3d-models/low-poly-stadiumsports-arena-seats-6bbe4c85d2a4489dbe5918831be5d886), CC-BY, 3,880 tris | 1 |
| 7 | Floodlight towers | Nothing suitable free. Trivial to model | **2** |

> ⚠️ **Legal risk, separate from copyright.** Sketchfab's cricket stadium models are almost all
> recognisable real venues — Narendra Modi Stadium, the SCG, Gaddafi Stadium, Ekana Lucknow. A
> CC-BY licence covers the *uploader's copyright in the model*. It does **not** cover the venue
> operator's trademarks, trade dress or name. Shipping a recognisable branded real stadium
> commercially is a distinct exposure. Keep the stadium generic.
>
> Also avoid Sketchfab's `Seating | Bleacher` (`6e0e88fa…`): the API reports CC-BY but the
> author's own description says "NOT ALLOWED TO UPLOAD IT ONLINE". A self-contradicting licence
> is not one to rely on.

## Bonus

Crowd ambience audio, CC-licensed: https://opengameart.org/content/free-crowd-cheering-sounds —
NOTES lists "Cheer audio on big shots & outs" as pending.
