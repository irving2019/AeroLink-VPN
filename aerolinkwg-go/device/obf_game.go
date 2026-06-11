package device

import (
	"encoding/binary"
	"encoding/hex"
	"errors"
	"strings"
	"sync/atomic"
)

// Глобальный счетчик для имитации игрового "тикрейта" (sequence)
var globalGameSeq uint32

func newGameObf(val string) (obf, error) {
	val = strings.TrimPrefix(val, "0x")
	if len(val) == 0 {
		val = "FFFFFFFF" // Дефолтный заголовок Source Engine (CS2 / Rust)
	}
	if len(val)%2 != 0 {
		return nil, errors.New("odd amount of symbols in game magic")
	}
	magic, err := hex.DecodeString(val)
	if err != nil {
		return nil, err
	}

	return &gameObf{
		magic: magic,
	}, nil
}

type gameObf struct {
	magic []byte
}

// Obfuscate добавляет магические байты игры и фейковый тикрейт
func (o *gameObf) Obfuscate(dst, src []byte) {
	copy(dst, o.magic)
	
	// Генерируем фейковый номер пакета (tickrate), чтобы трафик казался "живым"
	seq := atomic.AddUint32(&globalGameSeq, 1)
	binary.BigEndian.PutUint16(dst[len(o.magic):], uint16(seq))
}

func (o *gameObf) Deobfuscate(dst, src []byte) bool {
	if len(src) < len(o.magic) {
		return false
	}
	for i := range o.magic {
		if src[i] != o.magic[i] {
			return false
		}
	}
	return true
}

func (o *gameObf) ObfuscatedLen(n int) int {
	return len(o.magic) + 2 // Магические байты + 2 байта под тикрейт
}

func (o *gameObf) DeobfuscatedLen(n int) int {
	return 0
}