package main

import (
	"context"
	"database/sql"
	"errors"
	"time"

	"github.com/heroiclabs/nakama-common/runtime"
)

// The free Treasure Chest and the gem shop (ADR-043). The chest pays gold and Mana every few hours, more
// for a higher Dojo. The shop swaps gems for gold or Mana at fixed rates: nothing random, and no real
// money anywhere.

var (
	errChestNotReady = gameError("CHEST_NOT_READY", codePrecondition)
	errOfferNotFound = gameError("OFFER_NOT_FOUND", codeNotFound)
	errNoRoom        = gameError("StorageFull", codePrecondition)
)

type shopOffer struct {
	ID       string `json:"id"`
	Name     string `json:"name"`
	Crystals int64  `json:"crystals"`
	Gold     int64  `json:"gold"`
	Mana     int64  `json:"mana"`
}

type chestConfig struct {
	Hours            int64 `json:"hours"`
	GoldPerDojoLevel int64 `json:"goldPerDojoLevel"`
	ManaPerDojoLevel int64 `json:"manaPerDojoLevel"`
}

func (m *modesConfig) checkShop() error {
	if m.TreasureChest.Hours < 1 || m.TreasureChest.GoldPerDojoLevel < 0 || m.TreasureChest.ManaPerDojoLevel < 0 {
		return errors.New("treasure chest values are invalid")
	}
	seen := map[string]bool{}
	for _, offer := range m.Shop {
		if offer.ID == "" || seen[offer.ID] || offer.Crystals < 1 || offer.Gold < 0 || offer.Mana < 0 || offer.Gold+offer.Mana == 0 {
			return errors.New("shop offer " + offer.ID + " is invalid")
		}
		seen[offer.ID] = true
	}
	return nil
}

type chestView struct {
	ReadyAtMs int64       `json:"readyAtMs"`
	NowMs     int64       `json:"nowMs"`
	Gold      int64       `json:"gold"`
	Mana      int64       `json:"mana"`
	Offers    []shopOffer `json:"offers"`
	State     *playerState `json:"state,omitempty"`
}

func (s *Server) chestView(ctx context.Context, tx *sql.Tx, userID string, v *village) (*chestView, error) {
	var opened int64
	if err := tx.QueryRowContext(ctx, `SELECT chest_opened_ms FROM sd_player WHERE user_id = $1`, userID).Scan(&opened); err != nil {
		return nil, err
	}
	c := s.modes.TreasureChest
	hq := int64(v.hqLevel())
	ready := int64(0)
	if opened > 0 {
		ready = opened + c.Hours*3600000
	}
	return &chestView{ReadyAtMs: ready, NowMs: nowMillis(), Gold: c.GoldPerDojoLevel * hq, Mana: c.ManaPerDojoLevel * hq, Offers: s.modes.Shop}, nil
}

func (s *Server) rpcChestView(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		view, err := s.chestView(ctx, tx, userID, v)
		if err != nil {
			return err
		}
		output, err = marshal(view)
		return err
	})
	return output, err
}

// shopOp is the shared path for opening the chest and buying an offer: lock, replay check, change, persist.
func (s *Server) shopOp(ctx context.Context, db *sql.DB, payload, kind string, change func(tx *sql.Tx, userID string, v *village, offerID string) error) (string, error) {
	userID, err := userFrom(ctx)
	if err != nil {
		return "", err
	}
	var request struct {
		Key     string `json:"key"`
		OfferID string `json:"offerId"`
	}
	if err := decodeStrict(payload, &request); err != nil {
		return "", err
	}
	if !keyPattern.MatchString(request.Key) {
		return "", errBadKey
	}
	if !s.limiter.Allow(userID, time.Now()) {
		return "", errRateLimited
	}
	var output string
	err = withTx(ctx, db, func(tx *sql.Tx) error {
		crystals, err := s.lockPlayer(ctx, tx, userID)
		if err != nil {
			return err
		}
		if stored, found, err := replayed(ctx, tx, userID, request.Key, kind, request.OfferID); err != nil {
			return err
		} else if found {
			output = stored
			return nil
		}
		v, err := s.loadVillage(ctx, tx, userID, crystals)
		if err != nil {
			return err
		}
		beforeGold := v.gold
		if err := change(tx, userID, v, request.OfferID); err != nil {
			return err
		}
		if err := persistVillage(ctx, tx, userID, v, beforeGold, crystals, kind, request.Key); err != nil {
			return err
		}
		view, err := s.chestView(ctx, tx, userID, v)
		if err != nil {
			return err
		}
		if view.State, err = loadState(ctx, tx, userID, v.crystals); err != nil {
			return err
		}
		if output, err = marshal(view); err != nil {
			return err
		}
		return storeResponse(ctx, tx, userID, request.Key, kind, request.OfferID, output)
	})
	if err != nil {
		return "", err
	}
	return output, nil
}

func (s *Server) rpcChestOpen(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.shopOp(ctx, db, payload, "chest_open", func(tx *sql.Tx, userID string, v *village, offerID string) error {
		now := nowMillis()
		// Only one opening per wait: the row changes only if the wait is over.
		result, err := tx.ExecContext(ctx, `UPDATE sd_player SET chest_opened_ms = $1 WHERE user_id = $2 AND (chest_opened_ms = 0 OR chest_opened_ms + $3 <= $1)`,
			now, userID, s.modes.TreasureChest.Hours*3600000)
		if err != nil {
			return err
		}
		if changed, _ := result.RowsAffected(); changed != 1 {
			return errChestNotReady
		}
		hq := int64(v.hqLevel())
		payGold(v, s.modes.TreasureChest.GoldPerDojoLevel*hq)
		payMana(v, s.modes.TreasureChest.ManaPerDojoLevel*hq)
		return nil
	})
}

func (s *Server) rpcShopBuy(ctx context.Context, logger runtime.Logger, db *sql.DB, nk runtime.NakamaModule, payload string) (string, error) {
	return s.shopOp(ctx, db, payload, "shop_buy", func(tx *sql.Tx, userID string, v *village, offerID string) error {
		var offer *shopOffer
		for i := range s.modes.Shop {
			if s.modes.Shop[i].ID == offerID {
				offer = &s.modes.Shop[i]
			}
		}
		if offer == nil {
			return errOfferNotFound
		}
		if v.crystals < offer.Crystals {
			return errFunds
		}
		// Gold over the vault's room would be lost, so a swap that doesn't fit is refused instead.
		if offer.Gold > 0 && v.goldCapacity()-v.gold < offer.Gold {
			return errNoRoom
		}
		if offer.Mana > 0 && v.manaCapacity()-v.mana < offer.Mana {
			return errNoRoom
		}
		v.crystals -= offer.Crystals
		v.gold += offer.Gold
		v.mana += offer.Mana
		return nil
	})
}
