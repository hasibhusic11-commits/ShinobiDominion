package main

import (
	"strings"
	"testing"
)

func TestCleanName(t *testing.T) {
	good := map[string]string{"Kai": "Kai", "  Storm Fox  ": "Storm Fox", "ash_99": "ash_99", "Red-Moon": "Red-Moon"}
	for raw, want := range good {
		if got, ok := cleanName(raw); !ok || got != want {
			t.Errorf("%q: got %q %v, want %q", raw, got, ok, want)
		}
	}
	for _, bad := range []string{"", "ab", "a very long name indeed", "two  spaces", "-dash", "end_", "emoji😀", "tab\tname", "<script>"} {
		if _, ok := cleanName(bad); ok {
			t.Errorf("accepted %q", bad)
		}
	}
}

func TestValidGuildReportReason(t *testing.T) {
	for _, reason := range []string{"harassment", "hate", "spam", "other"} {
		if !validGuildReportReason(reason) { t.Fatalf("valid reason rejected: %s", reason) }
	}
	for _, reason := range []string{"", "HATE", "fraud", "other;DROP TABLE sd_guild", "self"} {
		if validGuildReportReason(reason) { t.Fatalf("invalid reason accepted: %s", reason) }
	}
}

func TestCleanChat(t *testing.T) {
	if got, ok := cleanChat("  hello\n\tworld  "); !ok || got != "hello world" {
		t.Fatalf("got %q", got)
	}
	if got, ok := cleanChat("bell\x07 ring"); !ok || got != "bell ring" {
		t.Fatalf("control characters should go: %q", got)
	}
	for _, bad := range []string{"", "   ", "\x00\x01", strings.Repeat("a", 201)} {
		if _, ok := cleanChat(bad); ok {
			t.Errorf("accepted %q", bad)
		}
	}
}
