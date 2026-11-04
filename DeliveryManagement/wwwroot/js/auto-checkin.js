/*
 * Tự động check-in vị trí cho shipper (trang DeliveryStaff/Details).
 * Theo dõi GPS của trình duyệt và gửi tọa độ lên server; server quyết định
 * có đang ở gần bưu cục nào hay không và tự ghi lượt check-in.
 * Lưu ý: trình duyệt chỉ cho dùng GPS trên HTTPS hoặc localhost.
 */
(function () {
    'use strict';

    const panel = document.getElementById('autoCheckInPanel');
    if (!panel || panel.dataset.active !== 'true') return;

    const statusEl = document.getElementById('autoCheckInStatus');
    const toggle = document.getElementById('autoCheckInToggle');
    const listEl = document.getElementById('autoCheckInList');
    const emptyEl = document.getElementById('autoCheckInEmpty');
    const tokenEl = panel.querySelector('input[name="__RequestVerificationToken"]');

    const SEND_INTERVAL_MS = 6000;    // tối thiểu 6 giây giữa hai lần gửi (bản đồ trực tiếp)
    const MAX_ACCURACY_M = 200;       // bỏ qua điểm GPS sai số lớn hơn 200 m

    let watchId = null;
    let lastSent = 0;
    let sending = false;
    let stopped = false;

    function setStatus(text, cls) {
        if (!statusEl) return;
        statusEl.textContent = text;
        statusEl.className = 'small mb-3 ' + (cls || 'text-muted');
    }

    function addHistoryItem(data) {
        if (!listEl) return;
        const li = document.createElement('li');
        li.className = 'list-group-item px-0';

        const name = document.createElement('strong');
        name.textContent = data.stationName || 'Bưu cục';

        const info = document.createElement('div');
        info.className = 'small text-muted';
        info.textContent = (data.time || '') + ' · '
            + Number(data.latitude).toFixed(5) + ', ' + Number(data.longitude).toFixed(5);

        li.appendChild(name);
        li.appendChild(info);
        listEl.insertBefore(li, listEl.firstChild);
        if (emptyEl) emptyEl.style.display = 'none';
    }

    async function send(position) {
        if (sending || stopped) return;
        const now = Date.now();
        if (now - lastSent < SEND_INTERVAL_MS) return;

        const c = position.coords;
        if (c.accuracy && c.accuracy > MAX_ACCURACY_M) {
            setStatus('Tín hiệu GPS yếu (sai số ±' + Math.round(c.accuracy) + ' m), đang chờ tín hiệu tốt hơn...', 'text-warning');
            return;
        }

        sending = true;
        lastSent = now;
        try {
            const res = await fetch(panel.dataset.url, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': tokenEl ? tokenEl.value : ''
                },
                body: JSON.stringify({
                    orderCode: panel.dataset.orderCode,
                    latitude: c.latitude,
                    longitude: c.longitude,
                    accuracy: c.accuracy
                })
            });

            let data = {};
            try { data = await res.json(); } catch (e) { /* phản hồi không phải JSON */ }

            if (res.status === 409 && data.stop) {
                stop();
                setStatus(data.message || 'Đã dừng check-in.', 'text-muted');
                return;
            }
            if (!res.ok) {
                setStatus(data.message || 'Không gửi được vị trí, sẽ thử lại.', 'text-danger');
                return;
            }

            if (data.checkedIn) {
                addHistoryItem(data);
                setStatus('Đã check-in tại ' + data.stationName + '.', 'text-success');
            } else if (data.alreadyCheckedIn) {
                setStatus('Bạn đang ở ' + data.stationName + '. Đã check-in lúc ' + (data.lastTime || '') + ', lượt mới sẽ được ghi sau ' + (data.waitMinutes || 5) + ' phút nếu bạn vẫn ở đây.', 'text-success');
            } else if (data.nearestStation) {
                setStatus('Đang theo dõi vị trí. Bưu cục gần nhất: ' + data.nearestStation
                    + ' (cách khoảng ' + Math.round(data.distanceMeters) + ' m).');
            } else {
                setStatus(data.message || 'Đang theo dõi vị trí...');
            }
        } catch (err) {
            setStatus('Mất kết nối, sẽ thử gửi lại vị trí.', 'text-danger');
        } finally {
            sending = false;
        }
    }

    function onError(err) {
        if (err.code === 1) {
            setStatus('Bạn chưa cho phép truy cập vị trí. Hãy cho phép trong cài đặt trình duyệt rồi bật lại.', 'text-danger');
            if (toggle) toggle.checked = false;
            stop();
        } else if (err.code === 2) {
            setStatus('Không xác định được vị trí. Hãy bật GPS.', 'text-warning');
        } else {
            setStatus('Hết thời gian chờ GPS, đang thử lại...', 'text-warning');
        }
    }

    function start() {
        if (watchId !== null) return;
        if (!('geolocation' in navigator)) {
            setStatus('Thiết bị/trình duyệt không hỗ trợ định vị.', 'text-danger');
            if (toggle) { toggle.checked = false; toggle.disabled = true; }
            return;
        }
        if (!window.isSecureContext) {
            setStatus('Trình duyệt chỉ cho dùng GPS trên HTTPS hoặc localhost.', 'text-danger');
            if (toggle) { toggle.checked = false; toggle.disabled = true; }
            return;
        }
        stopped = false;
        setStatus('Đang theo dõi vị trí...');
        watchId = navigator.geolocation.watchPosition(send, onError, {
            enableHighAccuracy: true,
            maximumAge: 10000,
            timeout: 20000
        });
    }

    function stop() {
        stopped = true;
        if (watchId !== null) {
            navigator.geolocation.clearWatch(watchId);
            watchId = null;
        }
    }

    if (toggle) {
        toggle.addEventListener('change', function () {
            if (toggle.checked) {
                lastSent = 0;
                start();
            } else {
                stop();
                setStatus('Đã tạm dừng check-in tự động.');
            }
        });
    }

    window.addEventListener('pagehide', stop);

    if (!toggle || toggle.checked) start();
})();
