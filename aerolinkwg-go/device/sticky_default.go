//go:build !linux

package device

import (
	"github.com/AeroLink/aerolinkwg-go/conn"
	"github.com/AeroLink/aerolinkwg-go/rwcancel"
)

func (device *Device) startRouteListener(_ conn.Bind) (*rwcancel.RWCancel, error) {
	return nil, nil
}
