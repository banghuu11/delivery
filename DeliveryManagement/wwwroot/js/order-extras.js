/*
 * Phần mở rộng cho trang tạo đơn (DeliveryOrder/Create):
 *  1) Khung QR thanh toán trực tuyến
 *  2) Nút "Xuất báo giá"
 * Nằm trong file riêng để Razor không xử lý, và lỗi ở đây không làm hỏng script chính của trang.
 */
(function () {
    'use strict';

    const $ = id => document.getElementById(id);
    const totalEl = $('totalPrice');
    const list = $('itemList');
    const form = document.querySelector('.order-form');
    if (!totalEl || !list || !form) return;

    const fmt = (n, d) => Number(n || 0).toLocaleString('vi-VN', { maximumFractionDigits: d == null ? 2 : d });
    const esc = v => String(v == null ? '' : v)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    const totalAmount = () => parseInt((totalEl.textContent || '').replace(/\D/g, ''), 10) || 0;
    const val = id => (($(id) || {}).value || '').trim();
    const named = n => ((document.querySelector('[name="' + n + '"]') || {}).value || '').trim();
    const checkedVal = n => { const r = document.querySelector('input[name="' + n + '"]:checked'); return r ? r.value : ''; };

    // ===== 1. QR THANH TOÁN TRỰC TUYẾN =====
    try {
        const box = $('paymentQrBox');
        if (box) {
            const hint = $('paymentQrHint');
            const content = $('paymentQrContent');
            const img = $('paymentQrImg');
            const amountEl = $('paymentQrAmount');

            const refreshQr = function () {
                const online = checkedVal('PaymentMethod') === 'Thanh toán trực tuyến';
                box.style.display = online ? 'block' : 'none';
                if (!online) return;

                const amount = totalAmount();
                if (amount <= 0) {
                    hint.style.display = 'block';
                    content.style.display = 'none';
                    return;
                }
                const d = box.dataset;
                img.src = 'https://img.vietqr.io/image/' + encodeURIComponent(d.bankCode) + '-'
                    + encodeURIComponent(d.accountNumber) + '-compact2.png?amount=' + amount
                    + '&addInfo=' + encodeURIComponent('Thanh toan don hang')
                    + '&accountName=' + encodeURIComponent(d.accountName);
                amountEl.textContent = fmt(amount, 0) + ' VNĐ';
                hint.style.display = 'none';
                content.style.display = 'block';
            };

            document.querySelectorAll('input[name="PaymentMethod"]').forEach(function (r) {
                r.addEventListener('change', refreshQr);
            });
            new MutationObserver(refreshQr).observe(totalEl, { childList: true, characterData: true, subtree: true });
            refreshQr();
        }
    } catch (err) {
        console.error('Lỗi khởi tạo QR thanh toán:', err);
    }

    // ===== 2. XUẤT BÁO GIÁ =====
    try {
        const btn = $('btnExportQuote');
        const hintEl = $('quoteHint');
        if (!btn) return;

        const collectItems = function () {
            return Array.prototype.slice.call(list.querySelectorAll('.item-row')).map(function (row) {
                const sel = row.querySelector('select');
                const q = row.querySelector('input[name$=".Quantity"]');
                const w = row.querySelector('input[name$=".Weight"]');
                const t = row.querySelector('textarea');
                return {
                    type: sel && sel.value ? sel.options[sel.selectedIndex].text : '',
                    desc: t ? t.value.trim() : '',
                    qty: Number(q && q.value) || 0,
                    weight: Number(w && w.value) || 0
                };
            });
        };

        const missing = function () {
            const m = [];
            if (!val('SenderNameInput')) m.push('tên người gửi');
            if (!val('SenderPhoneInput')) m.push('SĐT người gửi');
            if (!val('SenderAddressInput')) m.push('địa chỉ gửi');
            if (!named('ReceiverName')) m.push('tên người nhận');
            if (!named('ReceiverPhone')) m.push('SĐT người nhận');
            if (!val('ReceiverAddressInput')) m.push('địa chỉ nhận');
            const items = collectItems();
            if (!items.length || items.some(function (i) { return !i.type || i.qty <= 0 || i.weight <= 0; }))
                m.push('thông tin hàng hóa');
            if (totalAmount() <= 0) m.push('giá (chờ tính xong)');
            return m;
        };

        const refreshBtn = function () {
            const m = missing();
            btn.disabled = m.length > 0;
            hintEl.textContent = m.length
                ? 'Còn thiếu: ' + m.join(', ') + '.'
                : 'Đã đủ thông tin, có thể xuất báo giá.';
        };

        const buildQuote = function () {
            const now = new Date();
            const p2 = function (n) { return String(n).padStart(2, '0'); };
            const code = 'BG' + now.getFullYear() + p2(now.getMonth() + 1) + p2(now.getDate())
                + p2(now.getHours()) + p2(now.getMinutes()) + p2(now.getSeconds());
            const items = collectItems();
            const totalKg = items.reduce(function (s, i) { return s + i.qty * i.weight; }, 0);

            const itemRows = items.map(function (i, idx) {
                return '<tr><td class="c">' + (idx + 1) + '</td><td>' + esc(i.type) + '</td><td>' + (esc(i.desc) || '-')
                    + '</td><td class="r">' + i.qty + '</td><td class="r">' + fmt(i.weight)
                    + '</td><td class="r">' + fmt(i.qty * i.weight) + '</td></tr>';
            }).join('');

            const card = $('fareBreakdownCard');
            const t = function (id) { const e = $(id); return e ? e.textContent.trim() : ''; };
            let feeRows = '';
            if (card && card.style.display !== 'none') {
                [
                    ['Cước vận chuyển cơ bản (' + t('bdDistanceText') + ')', t('bdBaseFee')],
                    ['Phụ phí khối lượng (' + t('bdWeightText') + ')', t('bdWeightFee')],
                    ['Phụ thu thời tiết: ' + t('bdWeatherDesc'), t('bdWeatherFee')],
                    ['Mật độ giao thông: ' + t('bdTrafficDesc'), t('bdTrafficFee')]
                ].forEach(function (f) {
                    feeRows += '<tr><td colspan="5">' + esc(f[0]) + '</td><td class="r">' + esc(f[1]) + '</td></tr>';
                });
            }

            const dist = $('distanceDisplay');
            const css = 'body{font-family:Arial,Helvetica,sans-serif;color:#222;margin:0;padding:24px;background:#f3f4f6}'
                + '.page{max-width:800px;margin:0 auto;background:#fff;padding:32px 36px;box-shadow:0 2px 8px rgba(0,0,0,.1)}'
                + 'h1{text-align:center;margin:0 0 4px;font-size:22px;letter-spacing:1px}'
                + '.sub{text-align:center;color:#666;font-size:13px;margin-bottom:20px}'
                + '.cols{display:flex;gap:16px;margin-bottom:16px}'
                + '.box{flex:1;border:1px solid #ddd;border-radius:6px;padding:10px 12px;font-size:14px}'
                + '.box h3{margin:0 0 6px;font-size:14px;text-transform:uppercase;color:#0d6efd}'
                + 'table{width:100%;border-collapse:collapse;font-size:14px;margin-top:8px}'
                + 'th,td{border:1px solid #ccc;padding:6px 8px;vertical-align:top}'
                + 'th{background:#f0f4ff}.c{text-align:center}.r{text-align:right;white-space:nowrap}'
                + '.total td{font-weight:bold;font-size:16px;background:#fff7e6}'
                + '.meta{font-size:14px;margin:12px 0}.note{font-size:12px;color:#666;margin-top:16px}'
                + '.bar{max-width:800px;margin:0 auto 12px;text-align:right}'
                + '.bar button{padding:8px 18px;font-size:14px;cursor:pointer;margin-left:8px}'
                + '@media print{body{background:#fff;padding:0}.page{box-shadow:none;padding:0}.bar{display:none}}';

            return '<!DOCTYPE html><html lang="vi"><head><meta charset="utf-8"><title>Báo giá ' + code + '</title>'
                + '<style>' + css + '</style></head><body>'
                + '<div class="bar"><button onclick="window.print()">In / Lưu PDF</button>'
                + '<button onclick="window.close()">Đóng</button></div>'
                + '<div class="page"><h1>BÁO GIÁ DỊCH VỤ GIAO HÀNG</h1>'
                + '<div class="sub">Số: ' + code + ' &nbsp;|&nbsp; Ngày lập: ' + esc(now.toLocaleString('vi-VN')) + '</div>'
                + '<div class="cols">'
                + '<div class="box"><h3>Người gửi</h3>' + esc(val('SenderNameInput')) + '<br>' + esc(val('SenderPhoneInput'))
                + '<br>' + esc(val('SenderAddressInput')) + '</div>'
                + '<div class="box"><h3>Người nhận</h3>' + esc(named('ReceiverName')) + '<br>' + esc(named('ReceiverPhone'))
                + '<br>' + esc(val('ReceiverAddressInput')) + '</div></div>'
                + '<div class="meta">Hình thức giao hàng: <strong>' + esc(checkedVal('DeliveryMethod') || '-') + '</strong> &nbsp;|&nbsp; '
                + 'Thanh toán: <strong>' + esc(checkedVal('PaymentMethod') || '-') + '</strong> &nbsp;|&nbsp; '
                + 'Khoảng cách: <strong>' + esc(dist && dist.value ? dist.value : '-') + '</strong></div>'
                + '<table><thead><tr><th>STT</th><th>Loại đóng gói</th><th>Mô tả</th><th>SL</th><th>KL/món (kg)</th><th>Tổng KL (kg)</th></tr></thead>'
                + '<tbody>' + itemRows
                + '<tr><td colspan="5" class="r"><strong>Tổng khối lượng</strong></td><td class="r"><strong>' + fmt(totalKg) + ' kg</strong></td></tr>'
                + '</tbody></table>'
                + '<table><thead><tr><th colspan="5">Chi tiết cước phí</th><th>Thành tiền</th></tr></thead>'
                + '<tbody>' + feeRows
                + '<tr class="total"><td colspan="5" class="r">TỔNG CỘNG</td><td class="r">' + esc(fmt(totalAmount(), 0) + ' VNĐ') + '</td></tr>'
                + '</tbody></table>'
                + '<div class="note">Báo giá mang tính tham khảo, được tính theo khoảng cách, khối lượng và điều kiện thời tiết/giao thông '
                + 'tại thời điểm lập. Giá thực tế có thể thay đổi khi tạo đơn.</div></div></body></html>';
        };

        btn.addEventListener('click', function () {
            if (missing().length) return;
            const w = window.open('', '_blank');
            if (!w) {
                alert('Trình duyệt đang chặn cửa sổ bật lên. Hãy cho phép popup cho trang này rồi thử lại.');
                return;
            }
            w.document.open();
            w.document.write(buildQuote());
            w.document.close();
        });

        form.addEventListener('input', refreshBtn);
        form.addEventListener('change', refreshBtn);
        new MutationObserver(refreshBtn).observe(totalEl, { childList: true, characterData: true, subtree: true });
        new MutationObserver(refreshBtn).observe(list, { childList: true });
        refreshBtn();
    } catch (err) {
        console.error('Lỗi khởi tạo xuất báo giá:', err);
    }
})();
