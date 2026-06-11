package aerolinkcore

import (
	"bufio"
	"encoding/base64"
	"encoding/hex"
	"fmt"
	"net"
	"os"
	"regexp"
	"strings"
	"syscall"

	"github.com/AeroLink/aerolinkwg-go/conn"
	"github.com/AeroLink/aerolinkwg-go/device"
	"github.com/AeroLink/aerolinkwg-go/tun"
)

// --- НАШ СОБСТВЕННЫЙ АНДРОИД-ТУННЕЛЬ ---
type androidTun struct {
	file   *os.File
	events chan tun.Event
	mtu    int
}

func (t *androidTun) File() *os.File { return t.file }

func (t *androidTun) Read(bufs [][]byte, sizes []int, offset int) (int, error) {
	if len(bufs) == 0 {
		return 0, nil
	}
	n, err := t.file.Read(bufs[0][offset:])
	sizes[0] = n
	return 1, err
}

func (t *androidTun) Write(bufs [][]byte, offset int) (int, error) {
	for i, buf := range bufs {
		_, err := t.file.Write(buf[offset:])
		if err != nil {
			return i, err
		}
	}
	return len(bufs), nil
}

func (t *androidTun) Flush() error             { return nil }
func (t *androidTun) MTU() (int, error)        { return t.mtu, nil }
func (t *androidTun) Name() (string, error)    { return "AeroLink", nil }
func (t *androidTun) Events() <-chan tun.Event { return t.events }
func (t *androidTun) Close() error {
	close(t.events)
	return t.file.Close()
}
func (t *androidTun) BatchSize() int { return 1 }

// ----------------------------------------

var wgDevice *device.Device

func toHex(b64 string) string {
	dec, err := base64.StdEncoding.DecodeString(strings.TrimSpace(b64))
	if err != nil {
		return ""
	}
	return hex.EncodeToString(dec)
}

func parseToUAPI(configText string) string {
	var privateKey, publicKey, endpoint, presharedKey, keepalive string
	var allowedIps []string
	var awgParams []string

	// Регулярка для вытаскивания первого чистого числа (в т.ч. отрицательного)
	numRegex := regexp.MustCompile(`-?\d+`)

	scanner := bufio.NewScanner(strings.NewReader(configText))
	for scanner.Scan() {
		line := strings.TrimSpace(scanner.Text())
		
		// Пропускаем пустые строки, комментарии и заголовки секций
		if line == "" || strings.HasPrefix(line, "#") || strings.HasPrefix(line, "[") {
			continue
		}

		parts := strings.SplitN(line, "=", 2)
		if len(parts) != 2 {
			continue
		}

		key := strings.ToLower(strings.TrimSpace(parts[0]))
		val := strings.TrimSpace(parts[1])

		if val == "" {
			continue
		}

		switch key {
		case "privatekey":
			privateKey = toHex(val)
		case "publickey":
			publicKey = toHex(val)
		case "presharedkey":
			presharedKey = toHex(val)
		case "endpoint":
			// Пытаемся зарезолвить домен в IP-адрес
			host, port, err := net.SplitHostPort(val)
			if err == nil {
				ips, err := net.LookupIP(host)
				if err == nil && len(ips) > 0 {
					endpoint = net.JoinHostPort(ips[0].String(), port)
				} else {
					endpoint = val
				}
			} else {
				endpoint = val
			}
		case "persistentkeepalive":
			keepalive = val
		case "allowedips":
			ips := strings.Split(val, ",")
			for _, ip := range ips {
				allowedIps = append(allowedIps, strings.TrimSpace(ip))
			}
		case "jc", "jmin", "jmax", "s1", "s2", "s3", "s4":
			// Вытаскиваем только ПЕРВОЕ число, отсекая дефисы и мусор
			cleanNum := numRegex.FindString(val)
			if cleanNum != "" {
				awgParams = append(awgParams, fmt.Sprintf("%s=%s\n", key, cleanNum))
			}
		case "h1", "h2", "h3", "h4", "i1", "i2", "i3", "i4":
			if val != "" {
				awgParams = append(awgParams, fmt.Sprintf("%s=%s\n", key, val))
			}
		}
	}

	var uapi strings.Builder

	// Сборка конфига в правильном порядке
	if privateKey != "" {
		uapi.WriteString(fmt.Sprintf("private_key=%s\n", privateKey))
	}

	// Записываем параметры обфускации
	for _, p := range awgParams {
		uapi.WriteString(p)
	}

	uapi.WriteString("replace_peers=true\n")

	if publicKey != "" {
		uapi.WriteString(fmt.Sprintf("public_key=%s\n", publicKey))
		if endpoint != "" {
			uapi.WriteString(fmt.Sprintf("endpoint=%s\n", endpoint))
		}
		if presharedKey != "" {
			uapi.WriteString(fmt.Sprintf("preshared_key=%s\n", presharedKey))
		}
		if keepalive != "" {
			uapi.WriteString(fmt.Sprintf("persistent_keepalive_interval=%s\n", keepalive))
		}
		for _, ip := range allowedIps {
			uapi.WriteString(fmt.Sprintf("allowed_ip=%s\n", ip))
		}
	}

	return uapi.String()
}

func StartVPN(fd int, configText string) string {
	fmt.Printf("AeroLink Core: Инициализация кастомного туннеля. FD: %d\n", fd)

	err := syscall.SetNonblock(fd, true)
	if err != nil {
		return "ERROR: SetNonblock failed - " + err.Error()
	}

	tunFile := os.NewFile(uintptr(fd), "tun")
	if tunFile == nil {
		return "ERROR: Bad File Descriptor"
	}

	// Parse MTU from configText
	mtu := 1420
	for _, line := range strings.Split(configText, "\n") {
		line = strings.TrimSpace(line)
		if strings.HasPrefix(strings.ToLower(line), "mtu") {
			parts := strings.SplitN(line, "=", 2)
			if len(parts) == 2 {
				var parsedMtu int
				if _, err := fmt.Sscanf(strings.TrimSpace(parts[1]), "%d", &parsedMtu); err == nil && parsedMtu > 0 {
					mtu = parsedMtu
				}
			}
		}
	}

	tunDevice := &androidTun{
		file:   tunFile,
		events: make(chan tun.Event, 1),
		mtu:    mtu,
	}
	tunDevice.events <- tun.EventUp

	logger := device.NewLogger(device.LogLevelError, "AeroLinkCore")
	wgDevice = device.NewDevice(tunDevice, conn.NewDefaultBind(), logger)

	uapiConfig := parseToUAPI(configText)
	err = wgDevice.IpcSet(uapiConfig)
	if err != nil {
		return "ERROR: IpcSet failed - " + err.Error()
	}

	err = wgDevice.Up()
	if err != nil {
		return "ERROR: Up failed - " + err.Error()
	}

	return "SUCCESS"
}

func StopVPN() string {
	if wgDevice != nil {
		wgDevice.Close()
		wgDevice = nil
	}
	return "STOPPED"
}

func GetSocketFds() string {
	if wgDevice == nil {
		return ""
	}
	var fds []string
	if binder, ok := wgDevice.Bind().(conn.PeekLookAtSocketFd); ok {
		if fd, err := binder.PeekLookAtSocketFd4(); err == nil && fd != -1 {
			fds = append(fds, fmt.Sprintf("%d", fd))
		}
		if fd, err := binder.PeekLookAtSocketFd6(); err == nil && fd != -1 {
			fds = append(fds, fmt.Sprintf("%d", fd))
		}
	}
	return strings.Join(fds, ",")
}